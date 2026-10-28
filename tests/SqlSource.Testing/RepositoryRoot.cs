using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IOPath = System.IO.Path;

namespace SqlSource.Testing;

// The folder of the repository, found from the assembly that runs: the one that holds SqlSource.slnx.  For a test
// that reads the repository itself and not its output folder.  Every such read goes through Path, which refuses a
// container project a file whose scope in tools/test-scopes.txt would let a change to it skip that project.
internal static class RepositoryRoot
{
    private const string Solution = "SqlSource.slnx";
    private const string ScopesFileName = "tools/test-scopes.txt";

    private static readonly string Root = Find();

    // Under Microsoft.Testing.Platform the entry assembly is the test project itself.
    public static string RunningProject { get; } = Assembly.GetEntryAssembly()!.GetName().Name!;

    public static RepositoryWorkingFolder WorkingFolder => new(Root);

    // The one path that is not checked: the check reads its table from this file, so it cannot check the read.
    internal static string ScopesFile => IOPath.Combine(Root, ScopesFileName);

    // The full path of the parts under the root, or the root itself with none.  A path may go deeper than this from
    // there, never up, since only the path given here is checked.
    public static string Path(params string[] parts) => Path(RunningProject, TestScopes.Repository, parts);

    // The same for another project and table, for the tests of the check.
    internal static string Path(string project, TestScopes scopes, params string[] parts)
    {
        var full = IOPath.GetFullPath(IOPath.Combine([Root, .. parts]));
        var relative = IOPath.GetRelativePath(Root, full).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || IOPath.IsPathRooted(relative))
        {
            throw new ArgumentException($"'{string.Join('/', parts)}' is outside the repository.", nameof(parts));
        }

        // A project that always runs passes anyway, so it never pays for listing a folder.
        if (!scopes.Projects.ContainsKey(project))
        {
            return full;
        }

        if (!Directory.Exists(full))
        {
            Check(project, relative, scopes);
        }
        else
        {
            Check(project, relative == "." ? "" : relative + "/", scopes, FilesBelow(full));
        }

        return full;
    }

    // Throws when the scopes let a change to the path, or to a file below it, skip the project.  The paths are from the
    // root, and either separator matches as `/`.  A folder ends in `/`, so that it is decided as a file added to it
    // would be: a line `<folder>/*` covers it, where the folder's own name falls to the broader line after.
    public static void Check(
        string project,
        string relativePath,
        TestScopes scopes,
        IEnumerable<string>? filesBelow = null
    )
    {
        Refuse(project, relativePath, relativePath, scopes);
        foreach (var file in filesBelow ?? [])
        {
            Refuse(project, file, relativePath, scopes);
        }
    }

    private static void Refuse(string project, string path, string read, TestScopes scopes)
    {
        var scope = scopes.Of(path);
        if (scopes.Runs(project, scope))
        {
            return;
        }

        var below = path == read ? "" : $" under {read}";
        throw new InvalidOperationException(
            $"{project} reads {path.Replace('\\', '/')}{below}, whose scope in {ScopesFileName} is {scope}, so a "
                + $"change to it would skip {project}.  Read the file from the project's own scope, or give it a "
                + $"scope in {ScopesFileName} that runs {project}."
        );
    }

    // bin and obj are build output, not files of the repository, and their parent folder decides their scope anyway.
    private static IEnumerable<string> FilesBelow(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return IOPath.GetRelativePath(Root, file).Replace('\\', '/');
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (IOPath.GetFileName(child) is "bin" or "obj")
            {
                continue;
            }

            foreach (var file in FilesBelow(child))
            {
                yield return file;
            }
        }
    }

    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(IOPath.Combine(folder.FullName, Solution)))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No folder above '{AppContext.BaseDirectory}' holds {Solution}.");
    }
}
