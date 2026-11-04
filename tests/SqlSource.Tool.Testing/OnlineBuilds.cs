using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Testing;

// "dotnet build" of a copy of the fixture ErrorPaths of the engine's test project that goes online against the engine's
// server: the build runs the tool beside the tests before the compiler, and the generator beside the tests compiles the
// sidecar that the run created; a query that the server refuses fails the build at its file.  The build is given the
// connection as SQLSOURCE_CONNECTION, whose value it must never print.  The fixture's output is "models", whose types
// need no driver.  Each test makes the table of the query and drops it again.
internal sealed class OnlineBuilds(ErrorPathEngine engine, string connection) : IDisposable
{
    // The model of the query GetWidgets, named for the table that it reads whole.
    private const string Model = "ErrorPaths.ErrorPathWidgetsDto";

    private readonly FixtureProjects _projects = new();

    public void Dispose() => _projects.Dispose();

    // Builds a copy with no sidecar, and finds the sidecar written and the model of its query in the assembly.
    public async Task ShouldCompileTheSidecarItCreatedAsync()
    {
        var project = RealPath(_projects.Copy("ErrorPaths"));
        var folder = Path.GetDirectoryName(project)!;
        var sidecar = Path.Combine(folder, "Widgets.sql.json");
        File.Exists(sidecar).ShouldBeFalse();
        await engine.WithTableAsync(async () =>
        {
            var (exitCode, output) = await BuildAsync(project);

            exitCode.ShouldBe(0, output);
            File.Exists(sidecar).ShouldBeTrue(output);
            // Under bin/<configuration>/net10.0: CI sets CONFIGURATION, which MSBuild reads as the property.
            ShouldHoldTheModel(
                Directory
                    .GetFiles(Path.Combine(folder, "bin"), "App.dll", SearchOption.AllDirectories)
                    .Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "net10.0")
            );
        });
    }

    // Builds a copy with a query on a table that is not there, and finds the build failed by the tool's error, read
    // whole as one, at the query's name in its file.  The generator's error that the query has no entry follows it at
    // the same place, since the compiler still runs, and no error is anywhere else.
    public async Task ShouldFailAtTheRefusedQueryAsync()
    {
        var project = RealPath(_projects.Copy("ErrorPaths"));
        var refused = Path.Combine(Path.GetDirectoryName(project)!, "Refused.sql");
        await File.WriteAllTextAsync(
            refused,
            $"-- name: GetMissing\nSELECT id FROM {engine.MissingTable};\n",
            TestContext.Current.CancellationToken
        );
        await engine.WithTableAsync(async () =>
        {
            var (exitCode, output) = await BuildAsync(project);

            exitCode.ShouldBe(1, output);
            var errors = ErrorLines(output);
            _ = errors.Where(line => line.Contains("SQLSRC225", StringComparison.Ordinal)).ShouldHaveSingleItem(output);
            errors.ShouldAllBe(line => line.StartsWith(refused + "(1,10): error SQLSRC", StringComparison.Ordinal));
            File.Exists(refused + ".json").ShouldBeFalse();
        });
    }

    // Builds a copy whose settings file holds a value that is no output, and finds that the build lists the error of
    // the file once and says nothing of its own about the tool: the tool reads the same file and finds the same error
    // but leaves it to the compiler, and its exit code is not read as a tool that failed.  The compiler then reports
    // what follows from the file: here the query has the default output, whose method needs a driver.
    public async Task ShouldListABrokenSettingsFileOnceAsync()
    {
        var project = RealPath(_projects.Copy("ErrorPaths"));
        var settings = Path.Combine(Path.GetDirectoryName(project)!, "sqlsource.json");
        var text = await File.ReadAllTextAsync(settings, TestContext.Current.CancellationToken);
        text.ShouldContain("\"output\": \"models\"");
        await File.WriteAllTextAsync(
            settings,
            text.Replace("\"output\": \"models\"", "\"output\": \"nonsense\"", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );
        await engine.WithTableAsync(async () =>
        {
            var (exitCode, output) = await BuildAsync(project);

            exitCode.ShouldBe(1, output);
            var errors = ErrorLines(output);
            _ = errors.Where(line => line.Contains("SQLSRC019", StringComparison.Ordinal)).ShouldHaveSingleItem(output);
            errors.Where(static line => line.Contains("SQLSRC02", StringComparison.Ordinal)).ShouldBeEmpty(output);
        });
    }

    // "dotnet build" of the copy, online, with the tool and the generator beside the tests and the connection in the
    // environment.  The output is searched for the connection before any assertion can print it, so that a failure
    // never shows the value.
    private async Task<(int ExitCode, string Output)> BuildAsync(string project)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            project,
            new Dictionary<string, string> { ["SQLSOURCE_CONNECTION"] = connection },
            "-p:SqlSourceOnline=true",
            FixtureProjects.PropertySwitch("SqlSourceToolCommand", Programs.ToolCommand),
            FixtureProjects.PropertySwitch(
                "ErrorPathsGenerator",
                Path.Combine(AppContext.BaseDirectory, "SqlSource.dll")
            )
        );
        if (output.Contains(connection, StringComparison.Ordinal))
        {
            Assert.Fail("The build printed the value of SQLSOURCE_CONNECTION.");
        }

        return (exitCode, output);
    }

    // The errors of a build, each once: the console logger repeats them in its summary.
    private static string[] ErrorLines(string output) =>
        [
            .. output
                .Split('\n')
                .Select(line => line.TrimEnd('\r').Trim())
                .Where(line => line.Contains(": error ", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

    // Fails unless the assembly holds the model of GetWidgets, a sealed record of the table's two columns, neither of
    // which can be null.  The assembly is loaded into a context of its own that is unloaded again, so that the test
    // process keeps nothing of it.
    private static void ShouldHoldTheModel(string assembly)
    {
        var context = new AssemblyLoadContext("online-build", isCollectible: true);
        try
        {
            using var stream = File.OpenRead(assembly);
            var model = ModelReflection.ModelNamed(context.LoadFromStream(stream), Model).ShouldNotBeNull();
            ModelReflection.ShouldBeModel(
                model,
                Model,
                ModelShape.SealedRecord,
                ("Id", typeof(int), false),
                ("Name", typeof(string), false)
            );
        }
        finally
        {
            context.Unload();
        }
    }

    // The path with every link among its folders replaced by what it links to.  The temporary folder of macOS is under
    // /var, a link to /private/var, and a build of a project named through a link reports the tool's errors at the
    // path as MSBuild names it, under /var, and the compiler's under /private/var, where the compiler resolves a path
    // of the project's own.  The project is named by its real path, as a build started in its folder with no project
    // named would find it, so that the errors of both name a file alike.
    private static string RealPath(string path)
    {
        var real = Path.GetPathRoot(path)!;
        foreach (
            var name in path[real.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            real = Path.Combine(real, name);
            var target = new FileInfo(real).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                real = target.FullName;
            }
        }

        return real;
    }
}
