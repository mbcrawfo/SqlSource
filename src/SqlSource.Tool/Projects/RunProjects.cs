using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Finds the projects of a run and reads the manifest of each: the unit's projects, or the ones that
/// <c>--project</c> names, that use SqlSource.
/// </summary>
internal static class RunProjects
{
    /// <summary>
    /// The one comparer of project paths: two paths that differ only in case are one project.
    /// </summary>
    public static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;

    private static readonly ContinuationLine AddThePackage = new("help", "add the SqlSource package to the project");

    private static readonly ContinuationLine Restore = new("help", "run 'dotnet restore'");

    /// <summary>
    /// The run's projects, and the manifests of those that use SqlSource, in the order of the projects.  A project with
    /// an error is reported and has no manifest, and the run goes on with the rest.
    /// </summary>
    /// <param name="unit">The solution or the project the run is on.</param>
    /// <param name="named">
    /// The paths that <c>--project</c> gave, as they were typed.  Empty when it gave none.  A message names one by
    /// its position when it holds <c>=</c> or <c>;</c>.
    /// </param>
    /// <param name="host">The working directory, and what MSBuild is run with.</param>
    /// <param name="reporter">Where the errors go.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task<FoundProjects> FindAsync(
        RunUnit unit,
        IReadOnlyList<UsageValue> named,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<string> projects = [unit.Path];
        string? solution = null;
        if (unit.Kind == RunUnitKind.Solution)
        {
            var read = await SolutionReader.ReadAsync(unit.Path, cancellationToken);
            if (!read.Succeeded)
            {
                reporter.Report(ToolDiagnostic.ForFile(ToolDiagnostics.SolutionCannotBeRead, unit.Path, read.Failure));
                return FoundProjects.None;
            }

            projects = read.Value;
            solution = unit.Path;
        }

        var all = projects;
        var restricted = named.Count > 0;
        if (restricted)
        {
            projects = Restrict(projects, named, unit, host.WorkingDirectory, reporter);
        }

        // A project that --project leaves out is not asked about.
        foreach (var left in all.Except(projects, PathComparer))
        {
            RunEvents.Project(reporter.Trace, left, evaluation: null);
        }

        var manifests = ImmutableArray.CreateBuilder<ProjectManifest>();
        var assetsFiles = ImmutableArray.CreateBuilder<(string Project, string AssetsFile)>();
        foreach (var evaluation in await ProjectEvaluator.EvaluateAsync(host, projects, solution, cancellationToken))
        {
            RunEvents.Project(reporter.Trace, evaluation.ProjectPath, evaluation);
            var project = evaluation.ProjectPath;
            if (evaluation.AssetsFile is { } assetsFile)
            {
                assetsFiles.Add((project, assetsFile));
            }

            switch (evaluation.State)
            {
                case ProjectState.UsesSqlSource:
                    if (evaluation.Manifest is { } manifest)
                    {
                        manifests.Add(manifest);
                    }

                    break;

                case ProjectState.DoesNotUseSqlSource:
                    // A project of a solution that was not asked for by name is left out, and nothing is said:
                    // most solutions hold projects that have no SQL.
                    if (unit.Kind == RunUnitKind.Project || restricted)
                    {
                        reporter.Report(
                            ToolDiagnostic
                                .ForFile(ToolDiagnostics.ProjectDoesNotUseSqlSource, project)
                                .WithLines(AddThePackage)
                        );
                    }

                    break;

                case ProjectState.NotRestored:
                    // In a solution too: a run on a fresh checkout must not pass by finding nothing.
                    reporter.Report(
                        ToolDiagnostic.ForFile(ToolDiagnostics.ProjectNotRestored, project).WithLines(Restore)
                    );
                    break;

                case ProjectState.Failed:
                    if (evaluation.Failure is { } failure)
                    {
                        reporter.Report(failure);
                    }

                    break;

                default:
                    break;
            }
        }

        return new FoundProjects(projects, manifests.ToImmutable(), assetsFiles.ToImmutable());
    }

    /// <summary>
    /// The manifest of a run that the build started: read from the file the build wrote, with no question to MSBuild.
    /// A file that does not exist or cannot be read, or that is no manifest, is reported and gives none.
    /// </summary>
    /// <param name="manifest">
    /// The value of <c>--manifest</c>.  A message names the file by its position when the value holds <c>=</c> or
    /// <c>;</c>, which the build's path may, and then leaves out the system's reason, which quotes the path.
    /// </param>
    /// <param name="manifestPath">The full path of the manifest file.</param>
    /// <param name="host">Reads the file.</param>
    /// <param name="reporter">Where the error goes.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static Task<ImmutableArray<ProjectManifest>> FromManifestAsync(
        UsageValue manifest,
        string manifestPath,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? text;
        try
        {
            text = host.ReadTextFile(manifestPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var reason = manifest.MayBePrintedAsPath ? exception.Message : "the system would not read it";
            return Task.FromResult(Unreadable(manifest, manifestPath, reason, reporter));
        }

        if (text is null)
        {
            return Task.FromResult(Unreadable(manifest, manifestPath, "there is no such file", reporter));
        }

        var read = ManifestReader.Read(text);
        if (!read.Succeeded)
        {
            return Task.FromResult(Unreadable(manifest, manifestPath, read.Failure, reporter));
        }

        RunEvents.Project(
            reporter.Trace,
            read.Value.ProjectPath,
            new ProjectEvaluation(read.Value.ProjectPath, ProjectState.UsesSqlSource, read.Value)
        );
        return Task.FromResult<ImmutableArray<ProjectManifest>>([read.Value]);
    }

    // At the file, or at no file when the message names it by its position.
    private static ImmutableArray<ProjectManifest> Unreadable(
        UsageValue manifest,
        string manifestPath,
        string reason,
        Reporter reporter
    )
    {
        reporter.Report(
            manifest.MayBePrintedAsPath
                ? ToolDiagnostic.AtFile(
                    ToolDiagnostics.ManifestCannotBeRead,
                    manifestPath,
                    UsageValue.Quoted(manifestPath),
                    reason
                )
                : ToolDiagnostic.Create(
                    ToolDiagnostics.ManifestCannotBeRead,
                    $"at position {manifest.Position}",
                    reason
                )
        );
        return [];
    }

    // The projects of the unit that --project names, in the unit's order.  A path that names none is reported once.
    private static ImmutableArray<string> Restrict(
        ImmutableArray<string> projects,
        IReadOnlyList<UsageValue> named,
        RunUnit unit,
        string workingDirectory,
        Reporter reporter
    )
    {
        var selected = new HashSet<string>(PathComparer);
        var reported = new HashSet<string>(PathComparer);
        foreach (var name in named)
        {
            // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
            var path = name.Text.Length == 0 ? name.Text : Path.GetFullPath(name.Text, workingDirectory);
            if (projects.Contains(path, PathComparer))
            {
                _ = selected.Add(path);
            }
            else if (reported.Add(path))
            {
                reporter.Report(
                    ToolDiagnostic.Create(ToolDiagnostics.ProjectNotInRun, name.NamedAsPath(path), unit.Path)
                );
            }
        }

        return [.. projects.Where(selected.Contains)];
    }
}
