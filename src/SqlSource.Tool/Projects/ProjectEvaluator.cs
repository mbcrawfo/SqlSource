using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Asks MSBuild about the projects of a run: whether each uses SqlSource, and for its project manifest.
/// </summary>
/// <remarks>
/// <para>
/// Two runs of <c>dotnet msbuild</c> for a project, and three for one with several target frameworks.  The first
/// evaluates the project and runs no target; the last runs the target <c>SqlSourceWriteManifest</c> of the package,
/// which writes a file the tool named.  So the tool reads nothing that MSBuild prints but the JSON of
/// <c>-getProperty</c>, writes nothing into a project, and never restores or builds one.
/// </para>
/// <para>
/// Nothing is reported here.  An error travels in the <see cref="ProjectEvaluation" />, so that the caller reports
/// the errors of several projects in the order of the projects, and not of the processes.
/// </para>
/// </remarks>
internal static class ProjectEvaluator
{
    /// <summary>How many projects are asked about at once, at most.</summary>
    public const int MaxAtOnce = 8;

    /// <summary>How many lines of MSBuild's output an error shows.</summary>
    public const int MaxOutputLines = 20;

    /// <summary>The name of the target of the package that writes the manifest.</summary>
    public const string ManifestTarget = "SqlSourceWriteManifest";

    private const string PackageId = "SqlSource";

    private static readonly ImmutableDictionary<string, string> SetVariables = ImmutableDictionary
        .Create<string, string>(StringComparer.Ordinal)
        // A path outside ASCII arrives whole on Windows, where the console's code page is not UTF-8.  TD-0028.
        .Add("DOTNET_CLI_FORCE_UTF8_ENCODING", "true")
        // The first "dotnet" command on a machine prints a welcome, which would stand before the JSON.
        .Add("DOTNET_NOLOGO", "true");

    // A tool that a build started has these, and "dotnet" loads the SDK they name in place of the one that the
    // project's global.json picks.
    private static readonly ImmutableArray<string> RemovedVariables = ["MSBuildSDKsPath", "MSBuildExtensionsPath"];

    /// <summary>
    /// Asks about each project, several at once.
    /// </summary>
    /// <param name="host">The processes, the environment and the temporary directory.</param>
    /// <param name="projects">The full paths of the project files.</param>
    /// <param name="solution">The full path of the solution the projects are of, or null for a project alone.</param>
    /// <param name="cancellationToken">Ends the run: no further process is started.</param>
    /// <returns>One evaluation for each project, in the order of <paramref name="projects" />.</returns>
    public static async Task<ImmutableArray<ProjectEvaluation>> EvaluateAsync(
        ToolHost host,
        IReadOnlyList<string> projects,
        string? solution,
        CancellationToken cancellationToken
    )
    {
        if (projects.Count == 0)
        {
            return [];
        }

        // A folder of this run's own, for the manifests: nothing is written into a project.
        var folder = Directory
            .CreateDirectory(Path.Combine(host.TempDirectory, "sqlsource-" + Guid.NewGuid().ToString("N")))
            .FullName;
        try
        {
            var solutionSwitches = SolutionSwitches(solution);
            var evaluations = new ProjectEvaluation[projects.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(host.ProcessorCount, 1, MaxAtOnce),
                CancellationToken = cancellationToken,
            };
            await Parallel.ForEachAsync(
                Enumerable.Range(0, projects.Count),
                options,
                async (index, token) =>
                {
                    var manifestFile = Path.Combine(folder, index.ToString(CultureInfo.InvariantCulture) + ".manifest");
                    evaluations[index] = await EvaluateOneAsync(
                        host,
                        projects[index],
                        solutionSwitches,
                        manifestFile,
                        token
                    );
                }
            );
            return [.. evaluations];
        }
        finally
        {
            Delete(folder);
        }
    }

    /// <summary>
    /// The first of a project's <c>TargetFrameworks</c>, or null when it has none.
    /// </summary>
    internal static string? FirstFramework(string targetFrameworks) =>
        targetFrameworks
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    private static async Task<ProjectEvaluation> EvaluateOneAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> solutionSwitches,
        string manifestFile,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<string> evaluate =
        [
            "msbuild",
            project,
            "-nologo",
            "-getProperty:SqlSourceImported",
            "-getProperty:TargetFramework",
            "-getProperty:TargetFrameworks",
            "-getProperty:ProjectAssetsFile",
            "-getItem:PackageReference",
            .. solutionSwitches,
        ];

        var result = await RunAsync(host, project, evaluate, cancellationToken);
        if (!TryReadAnswer(result, out var answer, out var reason))
        {
            return Failed(project, reason, result);
        }

        // NuGet imports a package's props into a project with several frameworks only under a condition on
        // TargetFramework, so this answer says nothing but which framework is the first.  TD-0026.
        ImmutableArray<string> frameworkSwitch = [];
        if (answer.TargetFramework.Length == 0 && FirstFramework(answer.TargetFrameworks) is { } first)
        {
            frameworkSwitch = [MSBuildProperty.Switch("TargetFramework", first)];
            result = await RunAsync(host, project, [.. evaluate, .. frameworkSwitch], cancellationToken);
            if (!TryReadAnswer(result, out answer, out reason))
            {
                return Failed(project, reason, result);
            }
        }

        var state = StateOf(answer, project);
        if (state != ProjectState.UsesSqlSource)
        {
            return new ProjectEvaluation(project, state);
        }

        result = await RunAsync(
            host,
            project,
            [
                "msbuild",
                project,
                "-nologo",
                "-t:" + ManifestTarget,
                MSBuildProperty.Switch("SqlSourceManifestFile", manifestFile),
                .. frameworkSwitch,
                .. solutionSwitches,
            ],
            cancellationToken
        );
        if (result.ExitCode != 0)
        {
            return Failed(project, ExitCodeReason(result), result);
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(manifestFile, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(project, $"the target {ManifestTarget} left no file that can be read", result);
        }

        return ManifestReader.Read(text, out reason) is { } manifest
            ? new ProjectEvaluation(project, ProjectState.UsesSqlSource, manifest)
            : new ProjectEvaluation(
                project,
                ProjectState.Failed,
                Failure: ToolDiagnostic.ForFile(ToolDiagnostics.ManifestCannotBeRead, project, project, reason)
            );
    }

    private static Task<ProcessResult> RunAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> arguments,
        CancellationToken cancellationToken
    )
    {
        // The dotnet that started the tool, when it said which; else the one on the path.
        var dotnet = host.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

        // The project's folder, so that the project's own global.json picks the SDK.
        var workingDirectory = Path.GetDirectoryName(project) ?? host.WorkingDirectory;
        return host.Processes.RunAsync(
            new ProcessRequest(dotnet, arguments, workingDirectory, SetVariables, RemovedVariables),
            cancellationToken
        );
    }

    // What "dotnet build" of the solution gives each project.  A project that imports a file through
    // $(SolutionDir) evaluates with these as it does in a build.
    private static ImmutableArray<string> SolutionSwitches(string? solution)
    {
        if (solution is null)
        {
            return [];
        }

        var directory = Path.GetDirectoryName(solution) ?? "";
        if (!Path.EndsInDirectorySeparator(directory))
        {
            directory += Path.DirectorySeparatorChar;
        }

        return
        [
            MSBuildProperty.Switch("SolutionDir", directory),
            MSBuildProperty.Switch("SolutionPath", solution),
            MSBuildProperty.Switch("SolutionName", Path.GetFileNameWithoutExtension(solution)),
            MSBuildProperty.Switch("SolutionFileName", Path.GetFileName(solution)),
            MSBuildProperty.Switch("SolutionExt", Path.GetExtension(solution)),
        ];
    }

    // The first row that holds, of the table in the spec of sub-phase 2.3.
    private static ProjectState StateOf(Answer answer, string project)
    {
        if (string.Equals(answer.Imported, "true", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectState.UsesSqlSource;
        }

        // The reference is there and the package's props are not: no restore since it was added.
        if (answer.PackageReferences.Contains(PackageId, StringComparer.OrdinalIgnoreCase))
        {
            return ProjectState.NotRestored;
        }

        // A project with packages.config: NuGet writes it no assets file, so nothing says whether it was restored.
        // Such a project imports a package's props by a line of its own, so one that uses SqlSource has the marker.
        if (answer.ProjectAssetsFile.Length == 0)
        {
            return ProjectState.DoesNotUseSqlSource;
        }

        var assetsFile = Path.GetFullPath(answer.ProjectAssetsFile, Path.GetDirectoryName(project) ?? "");
        return File.Exists(assetsFile) ? ProjectState.DoesNotUseSqlSource : ProjectState.NotRestored;
    }

    private static bool TryReadAnswer(ProcessResult result, out Answer answer, out string reason)
    {
        answer = default;
        if (result.ExitCode != 0)
        {
            reason = ExitCodeReason(result);
            return false;
        }

        reason = "dotnet msbuild did not print the JSON of an evaluation";
        try
        {
            using var document = JsonDocument.Parse(result.Output);
            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("Properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object
                || !TryGetString(properties, "SqlSourceImported", out var imported)
                || !TryGetString(properties, "TargetFramework", out var targetFramework)
                || !TryGetString(properties, "TargetFrameworks", out var targetFrameworks)
                || !TryGetString(properties, "ProjectAssetsFile", out var assetsFile)
                || !document.RootElement.TryGetProperty("Items", out var items)
                || items.ValueKind != JsonValueKind.Object
                || !items.TryGetProperty("PackageReference", out var references)
                || references.ValueKind != JsonValueKind.Array
            )
            {
                return false;
            }

            var names = ImmutableArray.CreateBuilder<string>();
            foreach (var reference in references.EnumerateArray())
            {
                if (reference.ValueKind != JsonValueKind.Object || !TryGetString(reference, "Identity", out var name))
                {
                    return false;
                }

                names.Add(name.Trim());
            }

            answer = new Answer(
                imported.Trim(),
                targetFramework.Trim(),
                targetFrameworks,
                assetsFile.Trim(),
                names.ToImmutable()
            );
            reason = "";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? "";
            return true;
        }

        value = "";
        return false;
    }

    private static string ExitCodeReason(ProcessResult result) =>
        result.ExitCode == ProcessResult.NotStarted
            ? "dotnet could not be started"
            : string.Create(CultureInfo.InvariantCulture, $"dotnet msbuild ended with the exit code {result.ExitCode}");

    private static ProjectEvaluation Failed(string project, string reason, ProcessResult result)
    {
        // MSBuild writes the errors of an evaluation to its error output, and those of a target to the other.
        var lines = (result.Error + "\n" + result.Output)
            .Split('\n')
            .Select(static line => line.TrimEnd())
            .Where(static line => line.Length > 0)
            .Take(MaxOutputLines)
            .Select(static line => new ContinuationLine("msbuild", line));
        return new ProjectEvaluation(
            project,
            ProjectState.Failed,
            Failure: ToolDiagnostic
                .ForFile(ToolDiagnostics.ProjectCannotBeEvaluated, project, project)
                .WithLines([new ContinuationLine("reason", reason), .. lines])
        );
    }

    private static void Delete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder left in the temporary directory is no failure of the run.
        }
    }

    private readonly record struct Answer(
        string Imported,
        string TargetFramework,
        string TargetFrameworks,
        string ProjectAssetsFile,
        ImmutableArray<string> PackageReferences
    );
}
