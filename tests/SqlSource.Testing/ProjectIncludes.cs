using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SqlSource.Testing;

// The files that a test project's items name, read from the .csproj as MSBuild would resolve them.  For the tests that
// guard what a project may reach outside its folder.
internal static class ProjectIncludes
{
    private static readonly string[] FolderProperties = ["$(MSBuildThisFileDirectory)", "$(MSBuildProjectDirectory)"];

    // The Include of each item of the project that the filter accepts and that names a file outside the project's
    // folder, however it is spelled: the path is resolved as MSBuild resolves it, from the project's folder.
    public static IEnumerable<string> FromOutside(string projectFolder, XDocument project, Func<XElement, bool> items)
    {
        var folder = Path.GetFullPath(projectFolder) + Path.DirectorySeparatorChar;
        return project
            .Descendants()
            .Where(items)
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .Where(include => !Resolve(folder, include).StartsWith(folder, StringComparison.Ordinal));
    }

    // The files that an include names, as paths from the repository root with `/` as the separator.  With no `*` that
    // is the file itself.  Otherwise every file under the folder before the first `*` whose path matches, by the
    // matching of the test scopes.  That is slightly wider than MSBuild's `*`, which only checks more files.
    public static IReadOnlyList<string> Expand(string repositoryRoot, string projectFolder, string include)
    {
        var folder = Path.GetFullPath(projectFolder) + Path.DirectorySeparatorChar;
        var pattern = Path.GetRelativePath(repositoryRoot, Resolve(folder, include)).Replace('\\', '/');
        var star = pattern.IndexOf('*', StringComparison.Ordinal);
        if (star < 0)
        {
            return [pattern];
        }

        var root = Path.Combine(repositoryRoot, pattern[..(pattern.LastIndexOf('/', star) + 1)]);
        if (!Directory.Exists(root))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'))
                .Where(file => TestScopes.Matches(pattern, file))
                .Order(StringComparer.Ordinal),
        ];
    }

    // One line for each file that a container project includes from outside its folder and that the scopes let it
    // skip, as "<project>: <path> is <scope>".  A ProjectReference counts as the .csproj it names, and an element
    // that only removes or updates items names nothing.
    public static IReadOnlyList<string> Refused(TestScopes scopes, string repositoryRoot)
    {
        var refused = new List<string>();
        foreach (var projectFile in scopes.ProjectFiles)
        {
            var path = Path.Combine(repositoryRoot, projectFile);
            var project = Path.GetFileNameWithoutExtension(path);
            var folder = Path.GetDirectoryName(path)!;
            foreach (var include in FromOutside(folder, XDocument.Load(path), static _ => true))
            {
                foreach (var file in Expand(repositoryRoot, folder, include))
                {
                    var scope = scopes.Of(file);
                    if (!scopes.Runs(project, scope))
                    {
                        refused.Add($"{project}: {file} is {scope}");
                    }
                }
            }
        }

        return refused;
    }

    // MSBuild accepts either separator on every system, and the two properties name the project's folder.
    private static string Resolve(string folder, string include)
    {
        var path = include.Replace('\\', '/');
        foreach (var property in FolderProperties)
        {
            if (path.StartsWith(property, StringComparison.Ordinal))
            {
                path = path[property.Length..].TrimStart('/');
                break;
            }
        }

        return Path.GetFullPath(Path.Combine(folder, path));
    }
}
