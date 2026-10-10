using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Tests;

// A project in a temporary folder: C# and .sql files on the disk, and the manifest that the package's target would
// write for them, built here.  A test of the plan gives RunPlanner the manifest; a test of the command gives the
// runner the project, and the two runs of "dotnet msbuild" are answered from it.
internal sealed class TestProject
{
    private readonly TempFolder _folder;
    private readonly string _directory;
    private readonly List<string> _compile = [];
    private readonly List<(string Path, (string Name, string Value)[] Metadata)> _sql = [];

    public TestProject(TempFolder folder, string name = "App", string? directory = null)
    {
        _folder = folder;
        _directory = directory ?? name;
        ProjectPath = folder.WriteFile(Relative(name + ".csproj"), "<Project />");
    }

    public string ProjectPath { get; }

    public string LangVersion { get; set; } = "";

    public List<string> Constants { get; } = [];

    // The properties of the package, by the name MSBuild knows them by.
    public Dictionary<string, string> Properties { get; } = [with(StringComparer.Ordinal)];

    // The full path of a file of the project, from a path with forward slashes.
    public string PathOf(string relativePath) => _folder.PathOf(Relative(relativePath));

    // A C# file on the disk that the project compiles.
    public string AddSource(string relativePath, string text)
    {
        var path = _folder.WriteFile(Relative(relativePath), text);
        _compile.Add(path);
        return path;
    }

    // A .sql file on the disk that the project lists, with the metadata of its item.
    public string AddSql(string relativePath, string text, params (string Name, string Value)[] metadata)
    {
        var path = _folder.WriteFile(Relative(relativePath), text);
        _sql.Add((path, metadata));
        return path;
    }

    // The same with the bytes as they are, for a byte order mark.
    public string AddSqlBytes(string relativePath, byte[] bytes)
    {
        var path = _folder.WriteFile(Relative(relativePath));
        File.WriteAllBytes(path, bytes);
        _sql.Add((path, []));
        return path;
    }

    // A .sql file that the project lists and this helper does not write: another project's, or one that is gone.
    public void ListSql(string fullPath, params (string Name, string Value)[] metadata) =>
        _sql.Add((fullPath, metadata));

    public string ManifestText()
    {
        var text = new StringBuilder();
        _ = text.Append("SqlSourceManifest=1\n");
        _ = text.Append("Project=").Append(ProjectPath).Append('\n');
        _ = text.Append("TargetFramework=net10.0\n");
        _ = text.Append("LangVersion=").Append(LangVersion).Append('\n');
        _ = text.Append("DefineConstants=").AppendJoin(';', Constants).Append('\n');
        foreach (var (name, value) in Properties)
        {
            _ = text.Append("Property.").Append(name).Append('=').Append(value).Append('\n');
        }

        foreach (var (path, metadata) in _sql)
        {
            _ = text.Append("File=").Append(path).Append('\n');
            foreach (var (name, value) in metadata)
            {
                _ = text.Append("File.").Append(name).Append('=').Append(value).Append('\n');
            }
        }

        foreach (var path in _compile)
        {
            _ = text.Append("Compile=").Append(path).Append('\n');
        }

        return text.ToString();
    }

    public ProjectManifest Manifest() =>
        ManifestReader.Read(ManifestText(), out var reason) ?? throw new InvalidOperationException(reason);

    // Has the runner answer for this project with its manifest as it is when the target runs.
    public TestProject AnsweredBy(FakeProcessRunner runner)
    {
        runner.Projects[ProjectPath] = new FakeProject { ManifestWriter = ManifestText };
        return this;
    }

    private string Relative(string relativePath) =>
        _directory.Length == 0 ? relativePath : _directory + "/" + relativePath;
}
