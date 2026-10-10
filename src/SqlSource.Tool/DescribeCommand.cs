using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Tool.Describing;
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

    private const string DatabaseOption = "--database";

    internal const string ConnectionOption = "--connection";

    public static Command Create(ToolHost host, Reporter reporter)
    {
        // Several, so that System.CommandLine takes a .sql path beside the unit.  CheckUsage holds them to one unit.
        var path = new Argument<string[]>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one; the current directory when "
                + "left out.  A path that ends in .sql restricts the run to that file, and may be given several times.",
            Arity = ArgumentArity.ZeroOrMore,
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

        // Accepted and checked, and not shown: it selects queries, and nothing this version prints depends on which
        // are selected.  Sub-phase 2.5 gives it an effect and shows it.
        var database = new Option<string[]>(DatabaseOption)
        {
            Description = "A database to describe the queries of.  May be given several times.",
            HelpName = "name",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
            Hidden = true,
        };

        // Always a name, "=" and a value.  CheckUsage holds each to that, and a message never repeats one.
        var connection = new Option<string[]>(ConnectionOption)
        {
            Description =
                "The connection string of a database, as <name>=<connection string>.  May be given several times.",
            HelpName = "name=value",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var describe = new Command(Name, "Finds the queries of the projects that a database must describe")
        {
            path,
            project,
            database,
            connection,
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
    /// What is wrong with a command line of <c>describe</c> by the command's own rules, each a whole line to write.
    /// A line names a position and never repeats a token.  Empty for another command.
    /// </summary>
    /// <remarks>
    /// A path that ends in <c>.sql</c> is a filter and any other is the unit, of which there is one.  A value of
    /// <c>--database</c> is a database name, by the rule of the <c>-- database:</c> marker.  A value of
    /// <c>--connection</c> is a name, <c>=</c> and a value, and a name is given once.
    /// </remarks>
    public static IReadOnlyList<string> CheckUsage(Usage usage)
    {
        if (usage.Command.Name != Name)
        {
            return [];
        }

        var messages = new List<string>();
        var units = 0;
        var connections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in usage.Values)
        {
            if (value.Option is null)
            {
                if (!SqlPath.IsSqlFile(value.Text) && ++units > 1)
                {
                    messages.Add($"sqlsource: unexpected argument at position {value.Position}");
                }
            }
            else if (value.Option.Name == DatabaseOption && !SettingValue.IsDatabaseName(value.Text))
            {
                messages.Add(
                    $"sqlsource: the value of option '{DatabaseOption}' at position {value.Position} is not a "
                        + "database name"
                );
            }
            else if (value.Option.Name == ConnectionOption)
            {
                if (!ConnectionArgument.TryParse(value.Text, out var connection))
                {
                    messages.Add(
                        $"sqlsource: the value of option '{ConnectionOption}' at position {value.Position} is not "
                            + "<name>=<connection string>"
                    );
                }
                else if (!connections.Add(connection.Name))
                {
                    messages.Add(
                        $"sqlsource: option '{ConnectionOption}' at position {value.Position} names a database that "
                            + "an earlier one names"
                    );
                }
            }
        }

        return messages;
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
        // With file filters and no unit, the unit is found from the working directory, not from the files.
        var paths = parsed.GetValue<string[]>(PathArgument) ?? [];
        var unitPath = paths.FirstOrDefault(static path => !SqlPath.IsSqlFile(path));
        if (RunUnitFinder.Find(unitPath, host.WorkingDirectory, reporter) is not { } unit)
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

        var filters = RunFilters.Create(
            paths.Where(SqlPath.IsSqlFile),
            parsed.GetValue<string[]>(DatabaseOption) ?? [],
            host.WorkingDirectory
        );
        var result = RunPlanner.Plan(manifests, filters, cancellationToken);
        foreach (var error in result.Errors)
        {
            reporter.Report(error);
        }

        return result.Plan;
    }
}
