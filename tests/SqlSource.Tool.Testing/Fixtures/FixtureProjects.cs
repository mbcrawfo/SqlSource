using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tool.Testing.Fixtures;

// The projects of Fixtures/Projects, copied to a temporary folder outside the repository, so that nothing of the
// repository's own build applies to them.  The package's props and targets are in build/ beside the copies, where
// each project imports them from, the outer build's targets in buildMultiTargeting/, and the repository's global.json
// picks the SDK.
internal sealed class FixtureProjects : IDisposable
{
    private static readonly string Source = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly string[] ClearedForABuild =
    [
        "SQLSOURCE_LOG",
        "SqlSourceOnline",
        "SqlSourceEnvFile",
        "CI",
        "TF_BUILD",
        "TEAMCITY_VERSION",
        "JENKINS_URL",
        "GITHUB_ACTIONS",
        "MSBUILDLOGALLENVIRONMENTVARIABLES",
    ];

    private readonly TempFolder _folder = new();

    public FixtureProjects()
    {
        File.Copy(Path.Combine(Source, "global.json"), _folder.PathOf("global.json"));
        CopyFolder(Path.Combine(Source, "build"), _folder.PathOf("build"));
        CopyFolder(Path.Combine(Source, "buildMultiTargeting"), _folder.PathOf("buildMultiTargeting"));
    }

    // The folder that holds the copies.
    public string Root => _folder.Path;

    // Copies a fixture and gives the full path of a file of the copy, its project file when none is named.  The
    // copy is in a folder of the fixture's name, or of the name given.
    public string Copy(string fixture, string file = "App.csproj", string? folder = null)
    {
        var target = _folder.PathOf(folder ?? fixture);
        if (!Directory.Exists(target))
        {
            CopyFolder(Path.Combine(Source, "Projects", fixture), target);
        }

        return Path.Combine(target, file.Replace('/', Path.DirectorySeparatorChar));
    }

    // Copies the package's build/ to a folder of the name given beside the copies, as another version of the package
    // is in another folder of NuGet's cache, and gives its full path.
    public string CopyBuildFolder(string folder)
    {
        var target = _folder.PathOf(folder);
        CopyFolder(Path.Combine(Source, "build"), target);
        return target;
    }

    public string PathOf(string relativePath) => _folder.PathOf(relativePath);

    public string WriteFile(string relativePath, string content = "") => _folder.WriteFile(relativePath, content);

    // Runs the manifest target of a project with the real "dotnet msbuild", as the tool does, and gives the lines of
    // the file it wrote.  A property is given as "Name=value".
    public async Task<string[]> WriteManifestAsync(string project, params string[] properties)
    {
        var file = _folder.PathOf(Guid.NewGuid().ToString("N") + ".manifest");
        var (exitCode, output) = await RunMSBuildAsync(
            project,
            ["-t:SqlSourceWriteManifest", $"-p:SqlSourceManifestFile={file}", .. PropertySwitches(properties)]
        );
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"msbuild exited with {exitCode}:\n{output}");
        }

        return await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
    }

    public static async Task<(int ExitCode, string Output)> RunMSBuildAsync(
        string project,
        IEnumerable<string> arguments
    )
    {
        var result = await Programs.RunAsync(Start("msbuild", project, arguments), Eventually.Patience);
        return (result.ExitCode, result.Output + result.Error);
    }

    // Runs "dotnet build" on a project, which restores it first, and gives its exit code and what it wrote, with the
    // terminal logger off, so that each line is a line of the build, and with no MSBuild node left after it.  The build
    // has the test process's environment less every variable that could take it online, keep it offline as in CI, or
    // put a secret in its log, whatever the developer's shell or the runner has, and the variables given, which are
    // set after that and so win.
    public static async Task<(int ExitCode, string Output)> BuildAsync(
        string project,
        IReadOnlyDictionary<string, string> environment,
        params string[] arguments
    )
    {
        var start = Start("build", project, ["-tl:off", "-nr:false", .. arguments]);
        foreach (var name in start.Environment.Keys.Where(IsClearedForABuild).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        var result = await Programs.RunAsync(start, Eventually.Patience);
        return (result.ExitCode, result.Output + result.Error);
    }

    // The switch that gives a build a property whose value may hold any character of a path.  The value is escaped
    // for MSBuild, so that no character splits it or is read as a property, and MSBuild gives the property the value
    // as it was.
    public static string PropertySwitch(string name, string value)
    {
        var escaped = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || "/\\.-_: ".Contains(character, StringComparison.Ordinal))
            {
                _ = escaped.Append(character);
            }
            else if (character < 0x80)
            {
                _ = escaped.Append('%').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                _ = escaped.Append(character);
            }
        }

        return $"-p:{name}={escaped}";
    }

    public void Dispose() => _folder.Dispose();

    // A run of dotnet on a project, from the project's folder.
    private static ProcessStartInfo Start(string command, string project, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(Programs.Dotnet) { WorkingDirectory = Path.GetDirectoryName(project) };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-nologo");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A test process that MSBuild started has these, and "dotnet" would then load the SDK they name and not the
        // one global.json picks.
        _ = start.Environment.Remove("MSBuildSDKsPath");
        _ = start.Environment.Remove("MSBuildExtensionsPath");
        start.Environment["DOTNET_NOLOGO"] = "true";

        // MSBuild writes its own lines in the language of the user's interface, "0 Warnung(en)" in German, and the
        // tests read some of them, so every run of dotnet is in English, whatever the machine's language.
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        return start;
    }

    // The connections and SQLSOURCE_LOG, SqlSourceOnline and SqlSourceEnvFile, which MSBuild reads from the environment
    // as properties and the manual has users set there, the variables of the CI services that the package reads,
    // GitHub's for good measure, and the one that has MSBuild log every variable with its value.
    private static bool IsClearedForABuild(string name) =>
        name.StartsWith("SQLSOURCE_CONNECTION", StringComparison.OrdinalIgnoreCase)
        || ClearedForABuild.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> PropertySwitches(string[] properties)
    {
        foreach (var property in properties)
        {
            yield return "-p:" + property;
        }
    }

    private static void CopyFolder(string source, string target)
    {
        _ = Directory.CreateDirectory(target);
        foreach (var folder in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            _ = Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, folder)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
