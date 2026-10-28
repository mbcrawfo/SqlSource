using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SqlSource.Testing;

// Which database test projects a change to a path can affect.  None runs neither pair of container projects, and Both
// runs both.
internal enum TestScope
{
    None,
    Postgres,
    SqlServer,
    Both,
}

// tools/test-scopes.txt, read the same way by the tests and by pre-commit-validation.sh.  Its lines sort the paths of
// the repository into scopes; the first line that matches a path decides it, and a path that none matches is Both.  A
// pattern holds `*`, which matches any run of characters, `/` included, and every other character is literal: `?` and
// the brackets are refused, since bash would read them as glob characters and the two readers would disagree.  The
// `project` lines name the container projects and the engine each needs.
internal sealed class TestScopes
{
    private static readonly Lazy<TestScopes> RepositoryFile = new(() =>
        Parse(File.ReadAllText(RepositoryRoot.ScopesFile))
    );

    // Compiled once for each pattern, since a test asks about every pattern of the file for every file of the
    // repository.
    private static readonly ConcurrentDictionary<string, Regex> Matchers = new(StringComparer.Ordinal);

    private readonly List<(TestScope Scope, string Pattern, Regex Matcher)> _lines = [];
    private readonly List<(TestScope Scope, string Pattern)> _patterns = [];
    private readonly Dictionary<string, TestScope> _projects = [];
    private readonly List<string> _projectFiles = [];

    private TestScopes() { }

    public static TestScopes Repository => RepositoryFile.Value;

    public IReadOnlyList<(TestScope Scope, string Pattern)> Lines => _patterns;

    // The name of a project, the file name of its .csproj without the extension, and the engine it needs.
    public IReadOnlyDictionary<string, TestScope> Projects => _projects;

    public IReadOnlyList<string> ProjectFiles => _projectFiles;

    public static TestScopes Parse(string text)
    {
        var scopes = new TestScopes();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words[0] == "project")
            {
                scopes.AddProject(words, index + 1);
            }
            else
            {
                scopes.AddLine(words, index + 1);
            }
        }

        return scopes;
    }

    public static bool Matches(string pattern, string path) => Matcher(pattern).IsMatch(path);

    public TestScope Of(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        foreach (var (scope, _, matcher) in _lines)
        {
            if (matcher.IsMatch(path))
            {
                return scope;
            }
        }

        return TestScope.Both;
    }

    // A project that the file does not list always runs, as does one under a scope of Both.
    public bool Runs(string project, TestScope scope) =>
        scope == TestScope.Both || !_projects.TryGetValue(project, out var engine) || scope == engine;

    // \z and not $, which also matches before a final newline.
    private static Regex Matcher(string pattern) =>
        Matchers.GetOrAdd(
            pattern,
            static pattern =>
                new(
                    "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "\\z",
                    RegexOptions.Singleline
                )
        );

    private static TestScope? ScopeOf(string word) =>
        word switch
        {
            "none" => TestScope.None,
            "postgres" => TestScope.Postgres,
            "sqlserver" => TestScope.SqlServer,
            "both" => TestScope.Both,
            _ => null,
        };

    private void AddLine(string[] words, int lineNumber)
    {
        if (ScopeOf(words[0]) is not { } scope)
        {
            throw new FormatException(
                $"Test scopes line {lineNumber}: '{words[0]}' is not none, postgres, sqlserver or both."
            );
        }

        if (words.Length != 2)
        {
            throw new FormatException($"Test scopes line {lineNumber}: expected '<scope> <pattern>'.");
        }

        if (words[1].AsSpan().IndexOfAny("?[]") >= 0)
        {
            throw new FormatException(
                $"Test scopes line {lineNumber}: '{words[1]}' holds a glob character other than '*'."
            );
        }

        _lines.Add((scope, words[1], Matcher(words[1])));
        _patterns.Add((scope, words[1]));
    }

    private void AddProject(string[] words, int lineNumber)
    {
        if (words.Length != 3 || ScopeOf(words[1]) is not ({ } engine and (TestScope.Postgres or TestScope.SqlServer)))
        {
            throw new FormatException(
                $"Test scopes line {lineNumber}: expected 'project <postgres|sqlserver> <path>'."
            );
        }

        var name = Path.GetFileNameWithoutExtension(words[2]);
        if (!_projects.TryAdd(name, engine))
        {
            throw new FormatException($"Test scopes line {lineNumber}: the project '{name}' is already listed.");
        }

        _projectFiles.Add(words[2]);
    }
}
