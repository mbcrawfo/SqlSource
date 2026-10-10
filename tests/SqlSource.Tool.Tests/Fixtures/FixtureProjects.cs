using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SqlSource.Tool.Tests.Fixtures;

// The projects of Fixtures/Projects, copied to a temporary folder outside the repository, so that nothing of the
// repository's own build applies to them.  The package's props and targets are in build/ beside the copies, where
// each project imports them from, and the repository's global.json picks the SDK.
internal sealed class FixtureProjects : IDisposable
{
    private static readonly string Source = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private readonly TempFolder _folder = new();

    public FixtureProjects()
    {
        File.Copy(Path.Combine(Source, "global.json"), _folder.PathOf("global.json"));
        CopyFolder(Path.Combine(Source, "build"), _folder.PathOf("build"));
    }

    // The folder that holds the copies.
    public string Root => _folder.Path;

    // Copies a fixture and gives the full path of a file of the copy, its project file when none is named.
    public string Copy(string fixture, string file = "App.csproj")
    {
        var target = _folder.PathOf(fixture);
        if (!Directory.Exists(target))
        {
            CopyFolder(Path.Combine(Source, "Projects", fixture), target);
        }

        return Path.Combine(target, file.Replace('/', Path.DirectorySeparatorChar));
    }

    public string PathOf(string relativePath) => _folder.PathOf(relativePath);

    public string WriteFile(string relativePath, string content = "") => _folder.WriteFile(relativePath, content);

    // Runs the manifest target of a project with the real "dotnet msbuild", as the tool does, and gives the lines of
    // the file it wrote.  A property is given as "Name=value".
    public string[] WriteManifest(string project, params string[] properties)
    {
        var file = _folder.PathOf(Guid.NewGuid().ToString("N") + ".manifest");
        var (exitCode, output) = RunMSBuild(
            project,
            ["-t:SqlSourceWriteManifest", $"-p:SqlSourceManifestFile={file}", .. PropertySwitches(properties)]
        );
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"msbuild exited with {exitCode}:\n{output}");
        }

        return File.ReadAllLines(file);
    }

    public static (int ExitCode, string Output) RunMSBuild(string project, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        start.ArgumentList.Add("msbuild");
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

        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.GetAwaiter().GetResult());
    }

    public void Dispose() => _folder.Dispose();

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
