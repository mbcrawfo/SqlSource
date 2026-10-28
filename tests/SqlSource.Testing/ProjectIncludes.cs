using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SqlSource.Testing;

// The files that a test project's items name, read from its project files as MSBuild would resolve them.  For the
// tests that guard what a project may reach outside its folder.
internal static class ProjectIncludes
{
    private const string ThisFileFolder = "$(MSBuildThisFileDirectory)";
    private const string ProjectFolder = "$(MSBuildProjectDirectory)";
    private static readonly string[] DirectoryBuildFiles = ["Directory.Build.props", "Directory.Build.targets"];

    // Each path that an item of the project names outside the project's folder, however it is spelled: the Include of
    // an item that the filter accepts, split at `;`, or the Project of an Import.  The path is resolved as MSBuild
    // resolves it, from the project's folder.  One that holds a property the check does not know cannot be resolved,
    // so it counts as outside.  An item with only a Remove or an Update names nothing.
    public static IEnumerable<string> FromOutside(
        string projectFolder,
        XDocument project,
        Func<XElement, bool> items
    ) => Outside(projectFolder, projectFolder, project, items).Select(static name => name.Include);

    // The files that an include names, as paths from the repository root with `/` as the separator.  With no `*` that
    // is the file itself.  Otherwise every file under the folder before the first `*` whose path matches, by the
    // matching of the test scopes.  That is slightly wider than MSBuild's `*`, which only checks more files.
    public static IReadOnlyList<string> Expand(string repositoryRoot, string projectFolder, string include)
    {
        var folder = Path.GetFullPath(projectFolder);
        var full =
            Resolve(folder, folder, folder, include)
            ?? throw new ArgumentException($"'{include}' holds a property that cannot be resolved.", nameof(include));
        var pattern = Path.GetRelativePath(repositoryRoot, full).Replace('\\', '/');
        var star = pattern.IndexOf('*', StringComparison.Ordinal);
        if (star < 0)
        {
            return [pattern];
        }

        // MSBuild's `**/` also covers no folder at all, so `dir/**/*.sql` includes `dir/a.sql`.  Matching it as it is
        // would need a `/` after `dir` and miss that file; with `*`, which crosses `/`, both are found.
        var matching = pattern.Replace("**/", "*", StringComparison.Ordinal);
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
                .Where(file => TestScopes.Matches(matching, file))
                .Order(StringComparer.Ordinal),
        ];
    }

    // One line for each file that a container project includes or imports from outside its folder and that the scopes
    // let it skip, as "<project>: <path> is <scope>", and one for each path it cannot resolve.  A ProjectReference
    // counts as the .csproj it names.  The files read are those whose items reach the project: its .csproj, each
    // Directory.Build.props and .targets from its folder up to the root, and the same of each project under tests/
    // that it references, directly or not, each of which is judged from its own folder.
    public static IReadOnlyList<string> Refused(TestScopes scopes, string repositoryRoot)
    {
        var refused = new List<string>();
        foreach (var projectFile in scopes.ProjectFiles)
        {
            var path = Path.GetFullPath(Path.Combine(repositoryRoot, projectFile));
            var project = Path.GetFileNameWithoutExtension(path);
            foreach (var (file, folder) in Inputs(repositoryRoot, path))
            {
                var fileFolder = Path.GetDirectoryName(file)!;
                foreach (var (include, full) in Outside(fileFolder, folder, XDocument.Load(file), static _ => true))
                {
                    if (full is null)
                    {
                        refused.Add($"{project}: {include} holds a property the check cannot resolve");
                        continue;
                    }

                    foreach (var included in Expand(repositoryRoot, folder, full))
                    {
                        var scope = scopes.Of(included);
                        if (!scopes.Runs(project, scope))
                        {
                            refused.Add($"{project}: {included} is {scope}");
                        }
                    }
                }
            }
        }

        return refused;
    }

    // Each file read for the project, with the folder of the project that it is read for: the .csproj and the
    // Directory.Build files above it, of the project and of each library under tests/ that it references.  A project
    // outside tests/ is code under test, which reaches the project through its scope.  MSBuild imports only the nearest
    // Directory.Build.props, but one may import the next, so every one up to the root is read.  A file above two
    // projects is read for each, since its items resolve from the project's folder.
    private static IEnumerable<(string File, string ProjectFolder)> Inputs(string repositoryRoot, string projectFile)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var tests = Path.Combine(root, "tests") + Path.DirectorySeparatorChar;
        var read = new HashSet<(string, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { projectFile };
        var pending = new Queue<string>([projectFile]);
        while (pending.TryDequeue(out var current))
        {
            var folder = Path.GetDirectoryName(current)!;
            foreach (var file in DirectoryBuildChain(root, folder).Prepend(current))
            {
                if (read.Add((file, folder)))
                {
                    yield return (file, folder);
                }
            }

            foreach (var (_, reference) in Outside(folder, folder, XDocument.Load(current), IsReference))
            {
                if (reference?.StartsWith(tests, StringComparison.Ordinal) == true && seen.Add(reference))
                {
                    pending.Enqueue(reference);
                }
            }
        }
    }

    // Every Directory.Build.props and .targets from the folder up to the root.
    private static IEnumerable<string> DirectoryBuildChain(string root, string folder)
    {
        for (var up = new DirectoryInfo(folder); up is not null; up = up.Parent)
        {
            foreach (var file in DirectoryBuildFiles.Select(name => Path.Combine(up.FullName, name)).Where(File.Exists))
            {
                yield return file;
            }

            if (up.FullName == root)
            {
                yield break;
            }
        }
    }

    private static bool IsReference(XElement item) => item.Name.LocalName == "ProjectReference";

    // The names that the file's items give outside the project's folder, each with its full path, which is null when it
    // holds a property the check does not know.  An Import resolves from the file's folder and an item from the
    // project's, as MSBuild resolves them.
    private static IEnumerable<(string Include, string? Full)> Outside(
        string fileFolder,
        string projectFolder,
        XDocument file,
        Func<XElement, bool> items
    )
    {
        var inside = Path.GetFullPath(projectFolder) + Path.DirectorySeparatorChar;
        return file.Descendants()
            .Where(items)
            .SelectMany(item =>
            {
                var import = item.Name.LocalName == "Import";
                var from = import ? fileFolder : projectFolder;
                var value = (string?)item.Attribute(import ? "Project" : "Include") ?? "";
                return value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(include => (Include: include, Full: Resolve(from, fileFolder, projectFolder, include)));
            })
            .Where(name => name.Full is null || !name.Full.StartsWith(inside, StringComparison.Ordinal));
    }

    // MSBuild accepts either separator on every system, and the two properties name the folder of the file and of the
    // project.  A property other than those two stays in the path, where it would land inside the folder and be
    // missed, so the path is not resolved.
    private static string? Resolve(string from, string fileFolder, string projectFolder, string include)
    {
        var path = include.Replace('\\', '/');
        foreach (var (property, folder) in new[] { (ThisFileFolder, fileFolder), (ProjectFolder, projectFolder) })
        {
            if (path.StartsWith(property, StringComparison.Ordinal))
            {
                path = path[property.Length..].TrimStart('/');
                from = folder;
                break;
            }
        }

        return path.Contains("$(", StringComparison.Ordinal) ? null : Path.GetFullPath(Path.Combine(from, path));
    }
}
