using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
    /// its position when it may not be printed.
    /// </param>
    /// <param name="context">
    /// The working directory and what MSBuild is run with, where the errors go, and what ends the run.
    /// </param>
    public static async Task<FoundProjects> FindAsync(RunUnit unit, IReadOnlyList<UsageValue> named, RunContext context)
    {
        var (host, reporter, cancellationToken) = context;
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
                    manifests.AddRange(evaluation.Manifests);
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
                    throw UnexpectedEnumValueException.Of(evaluation.State);
            }
        }

        return new FoundProjects(projects, manifests.ToImmutable(), assetsFiles.ToImmutable());
    }

    /// <summary>
    /// The manifest of a run that the build started: read from the file the build wrote, with no question to MSBuild.
    /// A file that does not exist or cannot be read, or that is no manifest, is reported at its full path, and gives
    /// none.  The event of the project is the caller's, once every manifest is read.  A token of <c>--manifest</c>
    /// that may not be printed is named by its position instead, and the build's own path, under <c>--online</c>, is
    /// always printed.
    /// </summary>
    /// <param name="manifestPath">The full path of the manifest file.</param>
    /// <param name="given">The token of <c>--manifest</c>.</param>
    /// <param name="context">What reads the file, where the error goes, and what ends the run.</param>
    /// <param name="firstProject">
    /// The full path of the project of the first manifest of the run, when this is a later one: a manifest of another
    /// project is reported as one that cannot be read, and gives none.
    /// </param>
    public static Task<ImmutableArray<ProjectManifest>> FromManifestAsync(
        string manifestPath,
        UsageValue given,
        RunContext context,
        string? firstProject = null
    )
    {
        var (host, reporter, cancellationToken) = context;
        cancellationToken.ThrowIfCancellationRequested();
        string? text;
        try
        {
            text = host.ReadTextFile(manifestPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The system's message quotes the path.
            var reason = given.MayBePrintedAsPath ? exception.Message : "the system would not read it";
            return Task.FromResult(Unreadable(manifestPath, given, reason, reporter));
        }

        if (text is null)
        {
            return Task.FromResult(Unreadable(manifestPath, given, "there is no such file", reporter));
        }

        var read = ManifestReader.Read(text);
        if (!read.Succeeded)
        {
            return Task.FromResult(Unreadable(manifestPath, given, read.Failure, reporter));
        }

        if (firstProject is not null && !PathComparer.Equals(read.Value.ProjectPath, firstProject))
        {
            return Task.FromResult(
                Unreadable(
                    manifestPath,
                    given,
                    $"it is of '{read.Value.ProjectPath}', and the first manifest is of '{firstProject}'",
                    reporter
                )
            );
        }

        return Task.FromResult<ImmutableArray<ProjectManifest>>([read.Value]);
    }

    private static ImmutableArray<ProjectManifest> Unreadable(
        string manifestPath,
        UsageValue given,
        string reason,
        Reporter reporter
    )
    {
        reporter.Report(
            given.MayBePrintedAsPath
                ? ToolDiagnostic.AtFile(
                    ToolDiagnostics.ManifestCannotBeRead,
                    manifestPath,
                    UsageValue.Quoted(manifestPath),
                    reason
                )
                : ToolDiagnostic.Create(ToolDiagnostics.ManifestCannotBeRead, $"at position {given.Position}", reason)
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
