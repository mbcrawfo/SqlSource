using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Describing;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The steps of a run up to its plan, and its connections, which <c>describe</c>, each pass of a watch and
/// <c>diagnose</c> share.  Each step reports what it finds, which <c>Planning/</c>, whose plan they make, does not.
/// </summary>
internal static class PlanSteps
{
    /// <summary>
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project of it that uses SqlSource and could be read.
    /// </summary>
    /// <remarks>
    /// The summary of a run prints counts for each selected database and not which queries it selected, so a test
    /// reads the plan from here, and the run goes on from it.
    /// </remarks>
    internal static async Task<RunPlan?> PlanAsync(Usage usage, RunContext context) =>
        (await PlanUnitAsync(usage, context))?.Plan;

    /// <summary>
    /// The plan, as <see cref="PlanAsync" /> builds it, with the unit it was built for, whose folder holds the run's
    /// <c>.env</c>.
    /// </summary>
    internal static async Task<(RunUnit Unit, RunPlan Plan)?> PlanUnitAsync(Usage usage, RunContext context)
    {
        RunUnit unit;
        ImmutableArray<ProjectManifest> manifests;
        if (SharedOptions.ValuesOf(usage, DescribeCommand.ManifestOption) is [{ } manifestValue])
        {
            // The build has evaluated the project, so MSBuild is not asked again, and the project is the unit.
            manifests = await RunProjects.FromManifestAsync(
                Path.GetFullPath(manifestValue.Text, context.Host.WorkingDirectory),
                context
            );
            if (manifests.IsEmpty)
            {
                return null;
            }

            unit = new RunUnit(RunUnitKind.Project, manifests[0].ProjectPath);
            RunEvents.Unit(context.Reporter.Trace, unit, wasGiven: true);
        }
        else if (FindUnit(usage, context) is { } found)
        {
            unit = found;
            manifests = (await FindManifestsAsync(unit, usage, context)).Manifests;
        }
        else
        {
            return null;
        }

        return await PlanFromAsync(unit, manifests, usage, context) is { } plan ? (unit, plan) : null;
    }

    /// <summary>
    /// The unit that a command line runs on, from its path or from the working directory.  Null when there is none,
    /// which is reported.
    /// </summary>
    internal static RunUnit? FindUnit(Usage usage, RunContext context)
    {
        // With file filters and no unit, the unit is found from the working directory, not from the files.
        var unitPath = ArgumentsOf(usage).FirstOrDefault(static path => !SqlPath.IsSqlFile(path.Text));
        var found = RunUnitFinder.Find(unitPath, context.Host.WorkingDirectory);
        if (!found.Succeeded)
        {
            context.Reporter.Report(found.Failure);
            return null;
        }

        RunEvents.Unit(context.Reporter.Trace, found.Value, wasGiven: unitPath is not null);
        return found.Value;
    }

    /// <summary>
    /// The projects of the unit that the command line runs, which <c>--project</c> narrows, and their manifests: the
    /// evaluation of each, and its manifest target.  A project with an error is reported and has no manifest.
    /// </summary>
    internal static Task<FoundProjects> FindManifestsAsync(RunUnit unit, Usage usage, RunContext context) =>
        RunProjects.FindAsync(unit, SharedOptions.ValuesOf(usage, SharedOptions.Project), context);

    /// <summary>
    /// The plan of the manifests, which <c>--database</c> and the <c>.sql</c> paths narrow, with what is wrong with it
    /// reported.  Null when there is no manifest; a run with no error then says that no project uses SqlSource.
    /// </summary>
    internal static Task<RunPlan?> PlanFromAsync(
        RunUnit unit,
        ImmutableArray<ProjectManifest> manifests,
        Usage usage,
        RunContext context
    )
    {
        var isOnline = DescribeCommand.IsOnline(usage);
        return PlanFromAsync(
            unit,
            manifests,
            RunFilters.Create(
                ArgumentsOf(usage).Where(static path => SqlPath.IsSqlFile(path.Text)),
                SharedOptions.ValuesOf(usage, DescribeCommand.DatabaseOption),
                context.Host.WorkingDirectory
            ),
            (_, error) => isOnline && IsReportedByTheCompiler(error),
            context
        );
    }

    /// <summary>
    /// The plan of the manifests, which the filters narrow, with what is wrong with it reported.  Null when there is
    /// no manifest; a run with no error then says that no project uses SqlSource.  <c>diagnose</c> plans with the file
    /// of its query as the one filter.
    /// </summary>
    /// <param name="unit">The unit the manifests are of.</param>
    /// <param name="manifests">The manifests of its projects.</param>
    /// <param name="filters">What the run is restricted to.</param>
    /// <param name="isLoggedOnly">
    /// Whether an error of the plan is only counted and logged, and not written: under <c>--online</c>, an error that
    /// the compiler reports too, and for <c>diagnose</c>, by <see cref="IsAtAFileNotNamed" />, an error at a claimed
    /// <c>.sql</c> file that no file filter names, since it cannot change the one query of that run and
    /// <c>describe</c> prints it.  <c>diagnose</c>'s error at a named file, at a settings file or a C# file, or at no
    /// file, is written.
    /// </param>
    /// <param name="context">Standard output, where the errors go with the run's trace, and what ends the plan.</param>
    internal static async Task<RunPlan?> PlanFromAsync(
        RunUnit unit,
        ImmutableArray<ProjectManifest> manifests,
        RunFilters filters,
        Func<RunPlan, ToolDiagnostic, bool> isLoggedOnly,
        RunContext context
    )
    {
        var reporter = context.Reporter;
        if (manifests.IsEmpty)
        {
            if (reporter.Count == 0)
            {
                // A run that found nothing to do must not look like one that did it.
                await context.Host.Out.WriteLineAsync(
                    $"{Cli.LinePrefix}no project of '{OneLine.Of(unit.Path)}' uses SqlSource"
                );
            }

            return null;
        }

        var result = RunPlanner.Plan(manifests, filters, context.CancellationToken);
        for (var index = 0; index < manifests.Length; index++)
        {
            RunEvents.Unclaimed(reporter.Trace, manifests[index].ProjectPath, result.Unclaimed[index]);
        }

        foreach (var file in result.Plan.Files)
        {
            RunEvents.File(reporter.Trace, file);
        }

        // The build's compiler reports most of these too, at the same place, and the build lists the errors of both.
        // So under --online they are counted and logged and their lines are left out: the plan holds back the same
        // files and the run deletes nothing, as after any error, and the run exits 0 with its summary, since an exit
        // code of 1 with no error line would be a tool that failed.  diagnose does the same with an error at another
        // .sql file, so that the bundle it writes is not beside an exit code of 1.
        foreach (var error in result.Errors)
        {
            if (isLoggedOnly(result.Plan, error))
            {
                reporter.ReportWithoutWriting(error);
            }
            else
            {
                reporter.Report(error);
            }
        }

        return result.Plan;
    }

    /// <summary>
    /// Whether an error of the plan is at a claimed <c>.sql</c> file that no file filter names, which <c>diagnose</c>
    /// logs and does not write.
    /// </summary>
    internal static bool IsAtAFileNotNamed(RunPlan plan, ToolDiagnostic error) => IsAtOneOf(error, FilesNotNamed(plan));

    /// <summary>
    /// The full path of the <c>.env</c> file a run reads, or null when it reads none: the one <c>--env-file</c>
    /// names, or the one in <paramref name="unitFolder" /> when it names none.  <c>describe</c>, each pass of a watch
    /// and <c>diagnose</c> read it here.
    /// </summary>
    /// <param name="usage">
    /// The command line, whose <c>--env-file</c> <see cref="DescribeCommand.CheckUsage" /> has held to a path.
    /// </param>
    /// <param name="unitFolder">The folder of the run's unit.</param>
    /// <param name="host">The working folder, which a relative path is from.</param>
    internal static string? EnvFileOf(Usage usage, string unitFolder, ToolHost host) =>
        DotEnvSearch.FileOf(
            SharedOptions.ValuesOf(usage, SharedOptions.EnvFile) is [{ } value] ? value.Text : null,
            unitFolder,
            host.WorkingDirectory
        );

    /// <summary>
    /// The values of <c>--connection</c>, each with its position.  <see cref="DescribeCommand.CheckUsage" /> has held
    /// each to its form, as <c>diagnose</c>'s holds its own.
    /// </summary>
    internal static ImmutableArray<ConnectionArgument> ConnectionsOf(Usage usage)
    {
        var connections = ImmutableArray.CreateBuilder<ConnectionArgument>();
        foreach (var value in SharedOptions.ValuesOf(usage, SharedOptions.Connection))
        {
            connections.Add(
                ConnectionArgument.TryParse(value.Text, out var connection)
                    ? connection with
                    {
                        Position = value.Position,
                    }
                    : throw new InvalidOperationException("A --connection that is no name and value was let through.")
            );
        }

        return connections.ToImmutable();
    }

    /// <summary>
    /// The full path of the folder of the unit, whose <c>.env</c> the run reads.
    /// </summary>
    internal static string UnitFolderOf(RunUnit unit) =>
        Path.GetDirectoryName(unit.Path) ?? throw new InvalidOperationException("A unit is a file in a folder.");

    /// <summary>
    /// Where a run on the unit looks for its connections: the values of <c>--connection</c>, by
    /// <see cref="ConnectionsOf" />, the unit's folder, and the <c>.env</c> file of <see cref="EnvFileOf" />.
    /// <c>describe</c>, each pass of a watch and <c>diagnose</c> take them here.
    /// </summary>
    /// <param name="usage">The command line.</param>
    /// <param name="unit">The run's unit.</param>
    /// <param name="host">The working folder, which a relative path of <c>--env-file</c> is from.</param>
    internal static ConnectionSources SourcesOf(Usage usage, RunUnit unit, ToolHost host)
    {
        var unitFolder = UnitFolderOf(unit);
        return new ConnectionSources(ConnectionsOf(usage), unitFolder, EnvFileOf(usage, unitFolder, host));
    }

    // The arguments of the command: the unit, and the .sql paths.
    private static ImmutableArray<UsageValue> ArgumentsOf(Usage usage) =>
        [.. usage.Values.Where(static value => value.Option is null)];

    // The normalised paths of the planned files that no file filter names.
    private static HashSet<string> FilesNotNamed(RunPlan plan)
    {
        var named = new HashSet<string>(
            plan.Filters.Files.Select(static filter => filter.NormalizedPath).OfType<string>(),
            SqlPath.Comparer
        );
        return new HashSet<string>(
            plan.Files.Select(static file => file.NormalizedPath).Where(path => !named.Contains(path)),
            SqlPath.Comparer
        );
    }

    // Whether the error is at one of the files, by its normalised path: the planner gives a file's error at the path
    // it read, or at the normalised path the generator's reader was given.
    private static bool IsAtOneOf(ToolDiagnostic error, HashSet<string> normalizedPaths) =>
        error.Path is { } path && SqlPath.Normalize(path) is { } normalized && normalizedPaths.Contains(normalized);

    // The generator reports every SqlDiagnostics descriptor the planner finds, from the same code, and two of the
    // tool's own that SidecarChecker reports where the tool does (GeneratorParityTests compares them).  An error of one
    // of those descriptors that the generator does not report, a settings file's database, says so itself.
    private static bool IsReportedByTheCompiler(ToolDiagnostic error) =>
        !error.IsNotReportedByTheCompiler
        && (
            SqlDiagnostics.All.Contains(error.Descriptor)
            || ReferenceEquals(error.Descriptor, ToolDiagnostics.OutputNeedsDescribableDialect)
            || ReferenceEquals(error.Descriptor, ToolDiagnostics.TokenHasNoDefault)
        );
}
