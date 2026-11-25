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
using SqlSource.Tool.Describing;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Asks MSBuild about the projects of a run: whether each uses SqlSource, and for its project manifest.
/// </summary>
/// <remarks>
/// <para>
/// One run of <c>dotnet msbuild</c> evaluates a project and runs no target, and one more does the same under each of
/// several target frameworks.  A run for each framework that uses SqlSource runs the target
/// <c>SqlSourceWriteManifest</c> of the package, which writes a file the tool named.  So the tool reads nothing that
/// MSBuild prints but the JSON of <c>-getProperty</c> and <c>-getItem</c>, writes nothing into a project, and never
/// restores or builds one.
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

    /// <summary>
    /// The start of the name of the folder that an evaluation makes in <see cref="ToolHost.TempDirectory" /> for its
    /// manifests, which a <see cref="Guid" /> of 32 digits ends.  A watch knows its writes there by it.
    /// </summary>
    public const string ManifestFolderPrefix = "sqlsource-";

    /// <summary>The name of the target of the package that writes the manifest.</summary>
    public const string ManifestTarget = "SqlSourceWriteManifest";

    private const string PackageId = "SqlSource";

    private static readonly ImmutableDictionary<string, string> SetVariables = ImmutableDictionary
        .Create<string, string>(StringComparer.Ordinal)
        // A path outside ASCII arrives whole on Windows, where the console's code page is not UTF-8.
        .Add("DOTNET_CLI_FORCE_UTF8_ENCODING", "true")
        // The first "dotnet" command on a machine prints a welcome, which would stand before the JSON.
        .Add("DOTNET_NOLOGO", "true");

    private static readonly ImmutableArray<string> RemovedVariables =
    [
        // A tool that a build started has these, and "dotnet" loads the SDK they name in place of the one that the
        // project's global.json picks.
        "MSBuildSDKsPath",
        "MSBuildExtensionsPath",
        // A connection is for the describer alone: MSBuild reads the environment for properties, so a project's
        // targets would have it, and could print it.
        ConnectionVariables.Unnamed,
    ];

    // The variable of each database, for the same reason.
    private static readonly ImmutableArray<string> RemovedPrefixes = [ConnectionVariables.NamedPrefix];

    /// <summary>
    /// Asks about each project, several at once.
    /// </summary>
    /// <param name="host">The processes, the environment and the temporary directory.</param>
    /// <param name="projects">The full paths of the project files.</param>
    /// <param name="solution">The full path of the solution the projects are of, or null for a project alone.</param>
    /// <param name="configuration">
    /// The configuration to evaluate the projects in, as <c>dotnet build -c</c> gives it, or null for the one each
    /// project takes by default, which the environment can set.
    /// </param>
    /// <param name="cancellationToken">Ends the run: no further process is started.</param>
    /// <returns>One evaluation for each project, in the order of <paramref name="projects" />.</returns>
    public static async Task<ImmutableArray<ProjectEvaluation>> EvaluateAsync(
        ToolHost host,
        IReadOnlyList<string> projects,
        string? solution,
        string? configuration,
        CancellationToken cancellationToken
    )
    {
        if (projects.Count == 0)
        {
            return [];
        }

        // A folder of this run's own, for the manifests: nothing is written into a project.
        var folder = Directory
            .CreateDirectory(Path.Combine(host.TempDirectory, ManifestFolderPrefix + Guid.NewGuid().ToString("N")))
            .FullName;
        try
        {
            // What every run of MSBuild is given, the evaluation's and the target's: a property given on the command
            // line is global, and holds in each framework too.
            ImmutableArray<string> globalSwitches =
            [
                .. SolutionSwitches(solution),
                .. ConfigurationSwitches(configuration),
            ];
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
                        globalSwitches,
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
    /// The frameworks of a project's <c>TargetFrameworks</c>: split at <c>;</c>, trimmed, with no empty entry and no
    /// repeat, ignoring case, the first kept.
    /// </summary>
    internal static ImmutableArray<string> Frameworks(string targetFrameworks) =>
        [
            .. targetFrameworks
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

    private static async Task<ProjectEvaluation> EvaluateOneAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> globalSwitches,
        string manifestFile,
        CancellationToken cancellationToken
    )
    {
        // A solution may list a project that is not on the disk.  MSBuild is started in the project's folder, and
        // with the folder gone too nothing could be started, which would read as a machine without the SDK.
        if (!File.Exists(project))
        {
            return Failed(project, "the project file does not exist", new ProcessResult(0, "", ""));
        }

        var evaluate = EvaluateArguments(project, globalSwitches);
        var result = await RunAsync(host, project, evaluate, cancellationToken);
        if (!TryReadAnswer(result, out var answer, out var reason))
        {
            return Failed(project, reason, result);
        }

        var assetsFile = AssetsFileOf(answer, project);

        // NuGet imports a package's props into a project with several frameworks only under a condition on
        // TargetFramework, so this answer says nothing but which frameworks there are.
        var frameworks = answer.TargetFramework.Length != 0 ? [] : Frameworks(answer.TargetFrameworks);
        if (frameworks.IsEmpty)
        {
            var state = StateOf(answer, assetsFile);
            return state == ProjectState.UsesSqlSource
                ? await RunTargetAsync(host, project, manifestFile, [], globalSwitches, assetsFile, cancellationToken)
                : new ProjectEvaluation(project, state, AssetsFile: assetsFile);
        }

        return await EvaluateFrameworksAsync(
            host,
            project,
            frameworks,
            evaluate,
            globalSwitches,
            manifestFile,
            assetsFile,
            cancellationToken
        );
    }

    // Each framework is evaluated in turn, and the target runs for each that uses SqlSource, into a file of its own.
    private static async Task<ProjectEvaluation> EvaluateFrameworksAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> frameworks,
        ImmutableArray<string> evaluate,
        ImmutableArray<string> globalSwitches,
        string manifestFile,
        string? assetsFile,
        CancellationToken cancellationToken
    )
    {
        var manifests = ImmutableArray.CreateBuilder<ProjectManifest>();
        var directory = Path.GetDirectoryName(manifestFile) ?? "";
        var stem = Path.GetFileNameWithoutExtension(manifestFile);
        for (var index = 0; index < frameworks.Length; index++)
        {
            ImmutableArray<string> frameworkSwitch = [MSBuildProperty.Switch("TargetFramework", frameworks[index])];
            var result = await RunAsync(host, project, [.. evaluate, .. frameworkSwitch], cancellationToken);
            if (!TryReadAnswer(result, out var answer, out var reason))
            {
                return Failed(project, reason, result);
            }

            // A framework that was not restored makes the project not restored, as a failed one fails it: a plan of
            // the other frameworks alone could leave out what only that one needs.
            var state = StateOf(answer, assetsFile);
            if (state == ProjectState.NotRestored)
            {
                return new ProjectEvaluation(project, ProjectState.NotRestored, AssetsFile: assetsFile);
            }

            if (state != ProjectState.UsesSqlSource)
            {
                continue;
            }

            var file = Path.Combine(
                directory,
                stem + "-" + index.ToString(CultureInfo.InvariantCulture) + Path.GetExtension(manifestFile)
            );
            var evaluation = await RunTargetAsync(
                host,
                project,
                file,
                frameworkSwitch,
                globalSwitches,
                assetsFile,
                cancellationToken
            );
            if (evaluation.State != ProjectState.UsesSqlSource)
            {
                return evaluation;
            }

            manifests.AddRange(evaluation.Manifests);
        }

        var overall = manifests.Count != 0 ? ProjectState.UsesSqlSource : ProjectState.DoesNotUseSqlSource;
        return new ProjectEvaluation(project, overall, manifests.ToImmutable(), AssetsFile: assetsFile);
    }

    // Runs the target for one framework, or for the project's only one, and reads what it wrote.
    private static async Task<ProjectEvaluation> RunTargetAsync(
        ToolHost host,
        string project,
        string manifestFile,
        ImmutableArray<string> frameworkSwitch,
        ImmutableArray<string> globalSwitches,
        string? assetsFile,
        CancellationToken cancellationToken
    )
    {
        var target = await RunManifestTargetAsync(
            host,
            project,
            manifestFile,
            [.. frameworkSwitch, .. globalSwitches],
            cancellationToken
        );
        var evaluation = target.Succeeded
            ? await ReadManifestAsync(project, manifestFile, target.Value, cancellationToken)
            : target.Failure;
        return evaluation with { AssetsFile = assetsFile };
    }

    // The full path of the file that a restore of the project writes, or null for a project that has none, as one with
    // packages.config.
    private static string? AssetsFileOf(Answer answer, string project) =>
        answer.ProjectAssetsFile.Length == 0
            ? null
            : Path.GetFullPath(answer.ProjectAssetsFile, Path.GetDirectoryName(project) ?? "");

    // Evaluates the project and runs no target.
    private static ImmutableArray<string> EvaluateArguments(string project, ImmutableArray<string> globalSwitches) =>
        [
            "msbuild",
            project,
            "-nologo",
            "-getProperty:SqlSourceImported",
            "-getProperty:TargetFramework",
            "-getProperty:TargetFrameworks",
            "-getProperty:ProjectAssetsFile",
            "-getItem:PackageReference",
            .. globalSwitches,
        ];

    // Runs the target of the package that writes the manifest, to the file the tool named.
    private static async Task<Outcome<ProcessResult, ProjectEvaluation>> RunManifestTargetAsync(
        ToolHost host,
        string project,
        string manifestFile,
        ImmutableArray<string> switches,
        CancellationToken cancellationToken
    )
    {
        var result = await RunAsync(
            host,
            project,
            [
                "msbuild",
                project,
                "-nologo",
                "-t:" + ManifestTarget,
                MSBuildProperty.Switch("SqlSourceManifestFile", manifestFile),
                .. switches,
            ],
            cancellationToken
        );
        return result.ExitCode == 0
            ? Outcome<ProcessResult, ProjectEvaluation>.Of(result)
            : Outcome<ProcessResult, ProjectEvaluation>.Failed(Failed(project, ExitCodeReason(result), result));
    }

    // Reads the manifest the target wrote.  A file that is not there shows what the target printed.
    private static async Task<ProjectEvaluation> ReadManifestAsync(
        string project,
        string manifestFile,
        ProcessResult target,
        CancellationToken cancellationToken
    )
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(manifestFile, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(project, $"the target {ManifestTarget} left no file that can be read", target);
        }

        var read = ManifestReader.Read(text);
        return read.Succeeded
            ? new ProjectEvaluation(project, ProjectState.UsesSqlSource, [read.Value])
            : new ProjectEvaluation(
                project,
                ProjectState.Failed,
                Failure: ToolDiagnostic.AtFile(
                    ToolDiagnostics.ManifestCannotBeRead,
                    project,
                    $"of {UsageValue.Quoted(project)}",
                    read.Failure
                )
            );
    }

    private static Task<ProcessResult> RunAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> arguments,
        CancellationToken cancellationToken
    )
    {
        var dotnet = DotnetOf(host.GetEnvironmentVariable);

        // The project's folder, so that the project's own global.json picks the SDK.
        var workingDirectory = Path.GetDirectoryName(project) ?? host.WorkingDirectory;
        return host.Processes.RunAsync(
            new ProcessRequest(dotnet, arguments, workingDirectory, SetVariables, RemovedVariables, RemovedPrefixes),
            cancellationToken
        );
    }

    // The dotnet that started the tool, when it said which; else the one on the path.
    internal static string DotnetOf(Func<string, string?> getEnvironmentVariable) =>
        getEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

    // What "dotnet build -c" gives a project: Configuration as a global property, which the environment's variable of
    // that name does not override.  None without it, so that the project's default holds.
    private static ImmutableArray<string> ConfigurationSwitches(string? configuration) =>
        configuration is null ? [] : [MSBuildProperty.Switch("Configuration", configuration)];

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

    // The first that holds, in the order that "evaluating a project" in docs/architecture/tool.md gives.
    private static ProjectState StateOf(Answer answer, string? assetsFile)
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
        if (assetsFile is null)
        {
            return ProjectState.DoesNotUseSqlSource;
        }

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
        result.Started
            ? string.Create(CultureInfo.InvariantCulture, $"dotnet msbuild ended with the exit code {result.ExitCode}")
            : "dotnet could not be started";

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
                .ForFile(ToolDiagnostics.ProjectCannotBeEvaluated, project)
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
