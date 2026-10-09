using System;
using System.IO;

namespace SqlSource.Tool.Tests;

// A folder of a test's own, deleted with everything in it when the test ends.
internal sealed class TempFolder : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("sqlsource-tests-");

    public string Path => _directory.FullName;

    // The full path of a file or a folder under this one, from a path with forward slashes.
    public string PathOf(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string WriteFile(string relativePath, string content = "")
    {
        var path = PathOf(relativePath);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string CreateFolder(string relativePath) => Directory.CreateDirectory(PathOf(relativePath)).FullName;

    public void Dispose() => _directory.Delete(recursive: true);
}
