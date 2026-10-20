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
    /// The manifests of the run's projects, in the order of the projects.  A project with an error is reported and
    /// left out, and the run goes on with the rest.
    /// </summary>
    /// <param name="unit">The solution or the project the run is on.</param>
    /// <param name="named">
    /// The paths that <c>--project</c> gave, as they were typed.  Empty when it gave none.  A message names one by
    /// its position when it holds <c>=</c> or <c>;</c>.
    /// </param>
    /// <param name="host">The working directory, and what MSBuild is run with.</param>
    /// <param name="reporter">Where the errors go.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task<ImmutableArray<ProjectManifest>> FindAsync(
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
                return [];
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
        foreach (var evaluation in await ProjectEvaluator.EvaluateAsync(host, projects, solution, cancellationToken))
        {
            RunEvents.Project(reporter.Trace, evaluation.ProjectPath, evaluation);
            var project = evaluation.ProjectPath;
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

        return manifests.ToImmutable();
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
