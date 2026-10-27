using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using SqlSource.Testing.Generator;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Testing;

// A project in a temporary folder: C#, .sql and settings files on the disk, and the manifest that the package's target
// would write for them, built here.  A test of the plan gives RunPlanner the manifest; a test of the command gives the
// runner the project, and the two runs of "dotnet msbuild" are answered from it.
internal sealed class TestProject
{
    private readonly TempFolder _folder;
    private readonly string _directory;
    private readonly List<string> _compile = [];
    private readonly List<(string Path, (string Name, string Value)[] Metadata)> _sql = [];
    private readonly List<string> _settings = [];

    public TestProject(TempFolder folder, string name = "App", string? directory = null)
    {
        _folder = folder;
        _directory = directory ?? name;
        ProjectPath = folder.WriteFile(Relative(name + ".csproj"), "<Project />");
    }

    public string ProjectPath { get; }

    public string LangVersion { get; set; } = "";

    public List<string> Constants { get; } = [];

    // The properties of the package, by the name MSBuild knows them by, for the tests of the manifest's MSBuild values.
    // A test that only configures a project gives its settings in Settings or in a settings file.
    public Dictionary<string, string> Properties { get; } = [with(StringComparer.Ordinal)];

    // The project's own settings file, sqlsource.json in the folder of the project file, as a user writes it.  Whenever
    // it has a key, the file is written from it each time the manifest is, so that a change made after the runner was
    // told of the project still reaches the run, and listed before every other settings file, as the build lists the
    // files of the farther folders first.
    public JsonObject Settings { get; } = [];

    // The path of the project's own settings file.
    public string SettingsPath => PathOf("sqlsource.json");

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

    // A settings file on the disk that the project lists, as the build lists one above a .sql file or a C# file.
    public string AddSettings(string relativePath, string json)
    {
        if (Settings.Count > 0 && IsSettingsPath(PathOf(relativePath)))
        {
            throw AtSettingsPath();
        }

        var path = _folder.WriteFile(Relative(relativePath), json);
        _settings.Add(path);
        return path;
    }

    // A settings file that the project lists and this helper does not write: one above the project, or one that is
    // gone.
    public void ListSettings(string fullPath) => _settings.Add(fullPath);

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

        if (Settings.Count > 0)
        {
            if (_settings.Exists(IsSettingsPath))
            {
                throw AtSettingsPath();
            }

            File.WriteAllText(SettingsPath, GeneratorHarness.Json(Settings));
            _ = text.Append("Settings=").Append(SettingsPath).Append('\n');
        }

        foreach (var path in _settings)
        {
            _ = text.Append("Settings=").Append(path).Append('\n');
        }

        foreach (var path in _compile)
        {
            _ = text.Append("Compile=").Append(path).Append('\n');
        }

        return text.ToString();
    }

    public ProjectManifest Manifest()
    {
        var read = ManifestReader.Read(ManifestText());
        return read.Succeeded ? read.Value : throw new InvalidOperationException(read.Failure);
    }

    // Has the runner answer for this project with its manifest as it is when the target runs.
    public TestProject AnsweredBy(FakeProcessRunner runner)
    {
        runner.Projects[ProjectPath] = new FakeProject { ManifestWriter = ManifestText };
        return this;
    }

    // A path in any case is the same file on a disk that ignores case, as the disks of macOS and Windows do.
    private bool IsSettingsPath(string path) =>
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(SettingsPath), StringComparison.OrdinalIgnoreCase);

    private static InvalidOperationException AtSettingsPath() =>
        new(
            "The project writes its own settings file from Settings, and the test gives a file at that path.  Put its "
                + "keys in Settings instead."
        );

    private string Relative(string relativePath) =>
        _directory.Length == 0 ? relativePath : _directory + "/" + relativePath;
}
