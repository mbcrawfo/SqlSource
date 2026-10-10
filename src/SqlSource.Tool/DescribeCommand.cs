using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>describe</c> command.  After sub-phase 2.4 it finds the unit and the projects, builds the plan of the run
/// and reports what is wrong with it.  It describes nothing yet.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    private const string ProjectOption = "--project";

    public static Command Create(ToolHost host, Reporter reporter)
    {
        var path = new Argument<string?>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one.  The current directory "
                + "when left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        // One value each time it is given, and it may be given several times: UsageCheck reads the token after it as
        // its value and no further.
        var project = new Option<string[]>(ProjectOption)
        {
            Description = "A project to run on, of the solution.  May be given several times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var describe = new Command(Name, "Finds the queries of the projects that a database must describe")
        {
            path,
            project,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                _ = await PlanAsync(parsed, host, reporter, cancellationToken);
                return 0;
            }
        );
        return describe;
    }

    /// <summary>
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project that could be read.
    /// </summary>
    /// <remarks>
    /// Nothing that a run prints says what it selected, so a test reads the plan from here, and sub-phase 2.5 goes
    /// on from it.
    /// </remarks>
    internal static async Task<RunPlan?> PlanAsync(
        ParseResult parsed,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        if (RunUnitFinder.Find(parsed.GetValue<string?>(PathArgument), host.WorkingDirectory, reporter) is not { } unit)
        {
            return null;
        }

        var manifests = await RunProjects.FindAsync(
            unit,
            parsed.GetValue<string[]>(ProjectOption) ?? [],
            host,
            reporter,
            cancellationToken
        );
        if (manifests.IsEmpty)
        {
            if (reporter.Count == 0)
            {
                // A run that found nothing to do must not look like one that did it.
                await host.Out.WriteLineAsync($"sqlsource: no project of '{OneLine.Of(unit.Path)}' uses SqlSource");
            }

            return null;
        }

        var result = RunPlanner.Plan(manifests, cancellationToken);
        foreach (var error in result.Errors)
        {
            reporter.Report(error);
        }

        return result.Plan;
    }
}
