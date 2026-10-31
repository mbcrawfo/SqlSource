using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// tools/test-scopes.txt itself.  pre-commit-validation.sh trusts it to decide which database tests a change can skip,
// so a line that matches nothing, left behind by a renamed folder, would skip tests it should run.
public sealed class TestScopesFileTests
{
    // Folders that hold output or tooling state and no file the scopes sort.  Git is not asked, so that no test runs
    // it; a folder is skipped by its name, and the worktrees of an agent session by their place.
    private static readonly string[] SkippedFolders = [".git", "bin", "obj", "TestResults", "artifacts"];

    private const string AgentWorktrees = ".claude/worktrees";

    [Fact]
    public void RepositoryFile_EveryPattern_MatchesAFile()
    {
        var files = FilesOfTheRepository().ToList();

        var dead = TestScopes
            .Repository.Lines.Select(line => line.Pattern)
            .Where(pattern => !files.Any(file => TestScopes.Matches(pattern, file)))
            .ToList();

        dead.ShouldBeEmpty("These patterns match no file of the repository: " + string.Join(", ", dead));
    }

    // The first match wins, so the order of the lines is part of the meaning: an engine's fixtures and the folders of
    // its projects come before the broad `none` lines.  The scope is a name, since a public theory cannot take the
    // internal enum.
    [Theory]
    [InlineData("tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql", nameof(TestScope.Postgres))]
    [InlineData("tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql", nameof(TestScope.SqlServer))]
    [InlineData("tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/App.csproj", nameof(TestScope.Postgres))]
    [InlineData("tests/SqlSource.Tool.Tests/Fixtures/Projects/SqlServer/App.csproj", nameof(TestScope.SqlServer))]
    [InlineData("tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj", nameof(TestScope.None))]
    [InlineData("tests/DatabaseTests/Postgres/Postgres.csproj", nameof(TestScope.Postgres))]
    [InlineData("tests/DatabaseTests/Postgres.Npgsql8/AppContainer.cs", nameof(TestScope.Postgres))]
    [InlineData("tests/DatabaseTests/QueryMethodAllocation/QueryMethodAllocation.csproj", nameof(TestScope.None))]
    [InlineData("tests/DatabaseTests/Directory.Build.props", nameof(TestScope.Both))]
    [InlineData("tests/DatabaseTests/README.md", nameof(TestScope.None))]
    [InlineData("tests/DatabaseTests/TestHarness/EngineImage.cs", nameof(TestScope.Both))]
    [InlineData("tests/DatabaseTests/TestHarness/Postgis/Dockerfile", nameof(TestScope.Postgres))]
    public void Of_RealPath_IsDecidedAsIntended(string path, string scope) =>
        TestScopes.Repository.Of(path).ToString().ShouldBe(scope);

    [Fact]
    public void RepositoryFile_EveryProjectFile_Exists()
    {
        foreach (var projectFile in TestScopes.Repository.ProjectFiles)
        {
            File.Exists(RepositoryRoot.Path(projectFile)).ShouldBeTrue(projectFile);
        }
    }

    // TestScopes.ProjectOf puts every project line under TestScopes.AssemblyPrefix, which only
    // tests/DatabaseTests/Directory.Build.props gives an assembly, so a project listed elsewhere would never match
    // its own name and would read every file unchecked.
    [Fact]
    public void RepositoryFile_EveryProjectFile_IsUnderDatabaseTests() =>
        TestScopes.Repository.ProjectFiles.ShouldAllBe(file =>
            file.StartsWith("tests/DatabaseTests/", StringComparison.Ordinal)
        );

    [Fact]
    public void RepositoryFile_Projects_AreTheFourContainerProjects() =>
        TestScopes.Repository.Projects.Keys.ShouldBe(
            [
                "SqlSource.DatabaseTests.Postgres",
                "SqlSource.DatabaseTests.Postgres.Npgsql8",
                "SqlSource.DatabaseTests.SqlServer",
                "SqlSource.DatabaseTests.SqlServer.SqlClient61",
            ],
            ignoreOrder: true
        );

    // Every file under the root, as a path from it with `/` as the separator.
    private static IEnumerable<string> FilesOfTheRepository()
    {
        var root = new DirectoryInfo(RepositoryRoot.Path());
        var pending = new Stack<DirectoryInfo>([root]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            foreach (var file in folder.EnumerateFiles())
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
