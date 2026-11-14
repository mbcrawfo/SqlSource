using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.TestReview;

// The lists that the skill in .claude/skills/reviewing-test-quality/ keeps as files: the mutation scopes, the test
// projects, what no slice holds and the broad tests.  Each names folders, namespaces and classes, so a rename or a new
// folder would leave a part of the source unmeasured or a part of the tests unreviewed with nothing to say so.
public sealed partial class SkillDataTests
{
    private static readonly Dictionary<string, string> SourceProjects = new()
    {
        ["generator"] = "src/SqlSource",
        ["tool"] = "src/SqlSource.Tool",
    };

    // The folders of a source project that hold no code of the library to mutate, or only a build's output.
    private static readonly string[] NotMutated = ["Polyfills", "build", "buildMultiTargeting", "bin", "obj"];

    // The test folders of the two mutated projects' tests that mirror no source folder, and so are in no scope: the
    // tests of the test libraries, of the package's files and of whole builds.
    private static readonly string[] WithNoScope =
    [
        "tests/SqlSource.Tests/EndToEnd",
        "tests/SqlSource.Tests/Package",
        "tests/SqlSource.Tests/TestHarness",
        "tests/SqlSource.Tool.Tests/Fixtures",
        "tests/SqlSource.Tool.Tests/Realistic",
    ];

    private static readonly string[] TestData =
    [
        "tests/SqlSource.Tests/Snapshot/Examples/",
        "tests/SqlSource.Tool.Tests/Fixtures/Projects/",
    ];

    [GeneratedRegex(@"FullyQualifiedName!?~([\w.]+)")]
    private static partial Regex FilterClause();

    private sealed record Scope(string Name, string Project, string[] Mutate, string Tests, string[] Folders);

    private static string Root =>
        Path.GetDirectoryName(RepositoryRoot.Path("AGENTS.md"))!.Replace('\\', '/').TrimEnd('/');

    private static string[] Listed(string file) =>
        [
            .. File.ReadAllLines(RepositoryRoot.Path(".claude", "skills", "reviewing-test-quality", file))
                .Select(line => line.Split('#')[0].Trim())
                .Where(line => line.Length > 0),
        ];

    // The blocks of scopes.txt, read as measure.sh reads them: a key and its value to a line.
    private static List<Scope> Scopes()
    {
        var scopes = new List<Scope>();
        foreach (var line in Listed("scopes.txt"))
        {
            var (key, value) = (line.Split(' ')[0], line[(line.IndexOf(' ', StringComparison.Ordinal) + 1)..]);
            if (key == "scope")
            {
                scopes.Add(new Scope(value, "", [], "", []));
                continue;
            }

            scopes[^1] = key switch
            {
                "project" => scopes[^1] with { Project = value },
                "mutate" => scopes[^1] with { Mutate = value.Split(' ') },
                "tests" => scopes[^1] with { Tests = value },
                "folders" => scopes[^1] with { Folders = value.Split(' ') },
                _ => throw new InvalidDataException($"scopes.txt has the line '{line}', whose key is not known."),
            };
        }

        return scopes;
    }

    // The .cs files under a folder of the repository, from the root with '/', but a build's output.
    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory
            .EnumerateFiles(RepositoryRoot.Path(folder.Split('/')), "*.cs", SearchOption.AllDirectories)
            .Select(file => file.Replace('\\', '/')[(Root.Length + 1)..])
            .Where(file =>
                !file.Contains("/bin/", StringComparison.Ordinal) && !file.Contains("/obj/", StringComparison.Ordinal)
            );

    // Whether a glob of a scope names a file, the path of which is from its project's folder.  A scope's glob is one
    // of three forms, each written from the project's folder: "*.cs", the project's own files; "<folder>/*.cs", a
    // folder's own files; and "<folder>/**/*.cs", a folder's files at any depth.
    private static bool Matches(string glob, string path)
    {
        var folder = path.Contains('/', StringComparison.Ordinal) ? path[..path.LastIndexOf('/')] : "";
        if (glob == "*.cs")
        {
            return folder.Length == 0;
        }

        glob.ShouldEndWith("/*.cs");
        var named = glob[..^"/*.cs".Length];
        return named.EndsWith("/**", StringComparison.Ordinal)
            ? $"{folder}/".StartsWith(named[..^"**".Length], StringComparison.Ordinal)
            : folder == named;
    }

    // Stryker matches a glob against a file's full path as well as its path from the project, so a glob that starts
    // with "**/" also names every file of the project in a checkout under a folder of the glob's name: under
    // /Users/me/Projects/, "**/Projects/**/*.cs" is the whole tool.  A glob that starts at a folder of the project
    // cannot match a full path, which starts with '/' or a drive.
    [Fact]
    public void Scopes_EveryGlob_StartsAtAFolderOfTheProjectAndNotAtAnyFolder()
    {
        var loose = Scopes()
            .SelectMany(scope => scope.Mutate.Select(glob => $"{scope.Name}: {glob}"))
            .Where(glob =>
            {
                var written = glob[(glob.IndexOf(": ", StringComparison.Ordinal) + 2)..];
                return written != "*.cs" && !StartsAtAFolder().IsMatch(written);
            })
            .ToList();

        loose.ShouldBeEmpty("These globs do not start at a folder of the project: " + string.Join("; ", loose));
    }

    [GeneratedRegex(@"^\w+(/\w+)*(/\*\*)?/\*\.cs$")]
    private static partial Regex StartsAtAFolder();

    [Fact]
    public void Scopes_EveryBlock_IsWholeAndNamesFoldersThatExist()
    {
        var scopes = Scopes();

        scopes.ShouldNotBeEmpty();
        scopes.Select(scope => scope.Name).ShouldBeUnique();
        foreach (var scope in scopes)
        {
            SourceProjects.Keys.ShouldContain(scope.Project, scope.Name);
            scope.Mutate.ShouldNotBeEmpty(scope.Name);
            scope.Tests.ShouldStartWith("FullyQualifiedName~", customMessage: scope.Name);
            scope.Folders.ShouldNotBeEmpty(scope.Name);
            foreach (var folder in scope.Folders)
            {
                Directory.Exists(RepositoryRoot.Path(folder.Split('/'))).ShouldBeTrue($"{scope.Name}: {folder}");
            }
        }
    }

    [Theory]
    [InlineData("generator")]
    [InlineData("tool")]
    public void Scopes_EverySourceFile_IsMutatedByExactlyOneScope(string project)
    {
        var folder = SourceProjects[project];
        var scopes = Scopes().Where(scope => scope.Project == project).ToList();
        var files = SourceFiles(folder)
            .Select(file => file[(folder.Length + 1)..])
            .Where(file => !NotMutated.Contains(file.Split('/')[0]))
            .ToList();

        files.Count.ShouldBeGreaterThan(100);
        var wrong = files
            .Select(file => (File: file, Scopes: scopes.Where(scope => scope.Mutate.Any(glob => Matches(glob, file)))))
            .Where(file => file.Scopes.Count() != 1)
            .Select(file => $"{file.File}: {string.Join(", ", file.Scopes.Select(scope => scope.Name))}")
            .ToList();

        wrong.ShouldBeEmpty("These source files are in no scope or in several: " + string.Join("; ", wrong));
    }

    [Fact]
    public void Scopes_EveryGlob_NamesAFile()
    {
        var files = SourceProjects.ToDictionary(
            project => project.Key,
            project => SourceFiles(project.Value).Select(file => file[(project.Value.Length + 1)..]).ToList()
        );

        var dead = Scopes()
            .SelectMany(scope => scope.Mutate.Select(glob => (Scope: scope, Glob: glob)))
            .Where(glob => !files[glob.Scope.Project].Exists(file => Matches(glob.Glob, file)))
            .Select(glob => $"{glob.Scope.Name}: {glob.Glob}")
            .ToList();

        dead.ShouldBeEmpty("These globs name no source file: " + string.Join("; ", dead));
    }

    [Fact]
    public void Scopes_NoTestFolder_IsInTwoScopes() => Scopes().SelectMany(scope => scope.Folders).ShouldBeUnique();

    // A new folder of tests is given a scope, or is said here to have none, by whoever adds it.
    [Theory]
    [InlineData("tests/SqlSource.Tests")]
    [InlineData("tests/SqlSource.Tool.Tests")]
    public void Scopes_EveryTestFolderOfAMutatedProject_IsInAScopeOrIsKnownToHaveNone(string project)
    {
        var inScopes = Scopes().SelectMany(scope => scope.Folders).ToList();
        var folders = SourceFiles(project)
            .Where(file => !TestData.Any(data => file.StartsWith(data, StringComparison.Ordinal)))
            .Select(file => string.Join('/', file.Split('/').Take(3)))
            .Where(folder => !folder.EndsWith(".cs", StringComparison.Ordinal))
            .Distinct();

        var unplaced = folders
            .Where(folder =>
                !WithNoScope.Contains(folder)
                && !inScopes.Exists(scoped =>
                    scoped == folder || scoped.StartsWith(folder + "/", StringComparison.Ordinal)
                )
            )
            .ToList();

        unplaced.ShouldBeEmpty("These test folders are in no scope: " + string.Join(", ", unplaced));
        foreach (var folder in WithNoScope.Where(folder => folder.StartsWith(project + "/", StringComparison.Ordinal)))
        {
            Directory.Exists(RepositoryRoot.Path(folder.Split('/'))).ShouldBeTrue(folder);
            inScopes.ShouldNotContain(folder);
        }
    }

    // A filter that names a namespace or a class that was renamed runs no test, or runs tests it meant to leave out.
    [Fact]
    public void Scopes_EveryClauseOfAFilter_NamesANamespaceOrAClassOfTheTests()
    {
        var declared = DeclaredNames();

        var dead = Scopes()
            .SelectMany(scope =>
                FilterClause().Matches(scope.Tests).Select(clause => (Scope: scope.Name, Name: clause.Groups[1].Value))
            )
            .Where(clause => !declared.Contains(clause.Name))
            .Select(clause => $"{clause.Scope}: {clause.Name}")
            .ToList();

        dead.ShouldBeEmpty("These clauses name no namespace or class of the tests: " + string.Join("; ", dead));
    }

    [Fact]
    public void Projects_AndExclusions_TogetherHoldEveryProjectUnderTests()
    {
        var listed = Listed("projects.txt").Select(line => line.Split(' ')[0]).Concat(Listed("excluded.txt")).ToList();
        var projects = Directory
            .EnumerateFiles(RepositoryRoot.Path("tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(file => file.Replace('\\', '/')[(Root.Length + 1)..])
            .Where(file =>
                !TestData.Any(data => file.StartsWith(data, StringComparison.Ordinal))
                && !file.Contains("/bin/", StringComparison.Ordinal)
                && !file.Contains("/obj/", StringComparison.Ordinal)
            )
            .Select(file => file[..file.LastIndexOf('/')])
            .ToList();

        projects.Count.ShouldBeGreaterThan(10);
        projects.Where(project => !listed.Contains(project)).ShouldBeEmpty();
        Listed("projects.txt")
            .Select(line => line.Split(' ')[0])
            .Where(project => !projects.Contains(project))
            .ShouldBeEmpty();
    }

    // measure.sh knows no other mark, and runs a project with one it does not know as a plain one.
    [Fact]
    public void Projects_TheMarkedLines_AreTheSyncAsyncProjectsAndTheAllocationProject() =>
        Listed("projects.txt")
            .Where(line => line.Contains(' ', StringComparison.Ordinal))
            .ShouldBe([
                "tests/DatabaseTests/Postgres.SyncAsync sync-async",
                "tests/DatabaseTests/SqlServer.SyncAsync sync-async",
                "tests/DatabaseTests/QueryMethodAllocation not-run",
            ]);

    [Fact]
    public void Exclusions_EveryLine_IsAFolderThatExists() =>
        Listed("excluded.txt")
            .Where(folder => !Directory.Exists(RepositoryRoot.Path(folder.Split('/'))))
            .ShouldBeEmpty();

    [Fact]
    public void BroadTests_EveryLine_IsANamespaceOrAClassOfTheTestsWithItsDot()
    {
        var declared = DeclaredNames();
        var lines = Listed("broad-tests.txt");

        lines.ShouldNotBeEmpty();
        lines.Where(line => !line.EndsWith('.') || !declared.Contains(line)).ShouldBeEmpty();
    }

    // Every namespace that a file under tests/ declares and every class it declares at the start of a line, each
    // with a '.' after it, as a filter and a list of broad tests name them.
    private static HashSet<string> DeclaredNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles("tests"))
        {
            var text = File.ReadAllText(RepositoryRoot.Path(file.Split('/')));
            if (FileNamespace().Match(text) is not { Success: true } declared)
            {
                continue;
            }

            var space = declared.Groups[1].Value;
            _ = names.Add(space + ".");
            foreach (Match type in TopLevelClass().Matches(text))
            {
                _ = names.Add($"{space}.{type.Groups[1].Value}.");
            }
        }

        return names;
    }

    [GeneratedRegex(@"^namespace ([\w.]+);", RegexOptions.Multiline)]
    private static partial Regex FileNamespace();

    [GeneratedRegex(@"^(?:public|internal)(?: \w+)* class (\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelClass();
}
