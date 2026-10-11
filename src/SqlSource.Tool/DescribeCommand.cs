using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Generation.Paths;
using SqlSource.Settings;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>describe</c> command.  It finds the unit and the projects, builds the plan of the run and reports what is
/// wrong with it, and hands the plan to <see cref="DescribeRun" />, which describes and writes.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    private const string ProjectOption = "--project";

    private const string DatabaseOption = "--database";

    internal const string ConnectionOption = "--connection";

    private const string ForceOption = "--force";

    private const string CheckOption = "--check";

    private const string VerboseOption = "--verbose";

    internal const string LogOption = "--log";

    /// <summary>The environment variable that gives the log's path when <c>--log</c> does not.</summary>
    internal const string LogVariable = "SQLSOURCE_LOG";

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

        // It selects queries: what is described, what is written, and what the summary counts.
        var database = new Option<string[]>(DatabaseOption)
        {
            Description = "A database to describe the queries of.  May be given several times.",
            HelpName = "name",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
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

        var force = new Option<bool>(ForceOption)
        {
            Description =
                "Describe every query of the run again, whether or not its entry is current.  The command to run "
                + "after a change to the schema.",
            Arity = ArgumentArity.Zero,
        };

        var check = new Option<bool>(CheckOption)
        {
            Description =
                "Describe every query of the run and compare with the sidecars that are there.  Writes nothing, "
                + "and exits with 2 when a sidecar is out of date.  The command for a build server.",
            Arity = ArgumentArity.Zero,
        };

        var verbose = new Option<bool>(VerboseOption)
        {
            Description = "Say what the run decides for each project, file, database and query, as it goes.",
            Arity = ArgumentArity.Zero,
        };

        var log = new Option<string>(LogOption)
        {
            Description =
                "Write what the run did to this file, one JSON object on a line.  The file is overwritten.  "
                + $"{LogVariable} gives the path when the option does not.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

        var describe = new Command(Name, "Asks the databases of the projects to describe their queries")
        {
            path,
            project,
            database,
            connection,
            force,
            check,
            verbose,
            log,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                if (await PlanAsync(parsed, host, reporter, cancellationToken) is { } plan)
                {
                    await DescribeRun.RunAsync(plan, OptionsOf(parsed), host, reporter, cancellationToken);
                }

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
    /// <c>--connection</c> is a name, <c>=</c> and a value, and a name is given once.  On a line with a
    /// <c>--connection</c>, an argument that holds <c>=</c> or <c>;</c>, a <c>.sql</c> path too, is taken for the rest
    /// of a value that the shell split: it would be printed as a path.
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
        var hasConnection = usage.Values.Any(static value => value.Option?.Name == ConnectionOption);
        foreach (var value in usage.Values)
        {
            if (value.Option is null)
            {
                if (hasConnection && value.Text.AsSpan().IndexOfAny('=', ';') >= 0)
                {
                    messages.Add(
                        $"sqlsource: the argument at position {value.Position} looks like a part of a connection "
                            + $"string; put the value of '{ConnectionOption}' in quotes"
                    );
                }
                else if (!SqlPath.IsSqlFile(value.Text) && ++units > 1)
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
            else if (value.Option.Name == LogOption && string.IsNullOrWhiteSpace(value.Text))
            {
                // What --log "$UNSET" gives.
                messages.Add(
                    $"sqlsource: the value of option '{LogOption}' at position {value.Position} is not a path"
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
    /// Whether the command line runs <c>describe</c>: the command is named, and it is its own action that runs
    /// and not the help's.
    /// </summary>
    public static bool Runs(ParseResult parsed) =>
        parsed.CommandResult.Command.Name == Name
        && ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action);

    /// <summary>Whether a <c>describe</c> that runs was given <c>--verbose</c>.</summary>
    public static bool IsVerbose(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(VerboseOption);

    /// <summary>
    /// The path of the run's log as it was given, by <c>--log</c> or else by <c>SQLSOURCE_LOG</c>, or null when
    /// the run has none.  A command line that does not run <c>describe</c> has none: the help must not empty the
    /// log of the last run.  A variable that is empty or white space is not set.
    /// </summary>
    public static string? LogPathOf(ParseResult parsed, Func<string, string?> getEnvironmentVariable)
    {
        if (!Runs(parsed))
        {
            return null;
        }

        if (parsed.GetValue<string>(LogOption) is { } option)
        {
            return option;
        }

        var variable = getEnvironmentVariable(LogVariable);
        return string.IsNullOrWhiteSpace(variable) ? null : variable;
    }

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave <c>--connection</c> the same values, in the
    /// same order.  True for a command other than <c>describe</c>.
    /// </summary>
    /// <remarks>
    /// The log's <c>arguments</c> is redacted from what <see cref="UsageCheck" /> read, and the describer is handed
    /// what System.CommandLine bound.  If the two disagree on which token is a connection's value, a value could
    /// reach the log, so <c>Cli</c> runs nothing then.
    /// </remarks>
    public static bool ReadsConnectionsAs(Usage usage, ParseResult parsed) =>
        usage.Command.Name != Name
        || usage
            .Values.Where(static value => value.Option?.Name == ConnectionOption)
            .Select(static value => value.Text)
            .SequenceEqual(parsed.GetValue<string[]>(ConnectionOption) ?? [], StringComparer.Ordinal);

    // What the command line says of the run, beside its plan.  CheckUsage has held each --connection to its form.
    private static DescribeOptions OptionsOf(ParseResult parsed)
    {
        var paths = parsed.GetValue<string[]>(PathArgument) ?? [];
        var projects = parsed.GetValue<string[]>(ProjectOption) ?? [];
        var databases = parsed.GetValue<string[]>(DatabaseOption) ?? [];
        var connections = ImmutableArray.CreateBuilder<ConnectionArgument>();
        foreach (var text in parsed.GetValue<string[]>(ConnectionOption) ?? [])
        {
            connections.Add(
                ConnectionArgument.TryParse(text, out var connection)
                    ? connection
                    : throw new InvalidOperationException("A --connection that is no name and value was let through.")
            );
        }

        return new DescribeOptions(
            parsed.GetValue<bool>(ForceOption),
            parsed.GetValue<bool>(CheckOption),
            projects.Length > 0 || databases.Length > 0 || paths.Any(SqlPath.IsSqlFile),
            connections.ToImmutable(),
            [.. databases.Distinct(StringComparer.OrdinalIgnoreCase)]
        );
    }

    /// <summary>
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project that could be read.
    /// </summary>
    /// <remarks>
    /// The summary of a run prints counts for each selected database and not which queries it selected, so a test
    /// reads the plan from here, and the run goes on from it.
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
        var found = RunUnitFinder.Find(unitPath, host.WorkingDirectory);
        if (!found.Succeeded)
        {
            reporter.Report(found.Failure);
            return null;
        }

        var unit = found.Value;
        RunEvents.Unit(reporter.Trace, unit, wasGiven: unitPath is not null);
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
        foreach (var manifest in manifests)
        {
            var claimed = result.Plan.Files.Count(file =>
                file.Claims.Any(claim =>
                    string.Equals(claim.ProjectPath, manifest.ProjectPath, StringComparison.Ordinal)
                )
            );
            RunEvents.Unclaimed(reporter.Trace, manifest.ProjectPath, ListedFiles.Read(manifest).Paths.Count - claimed);
        }

        foreach (var file in result.Plan.Files)
        {
            RunEvents.File(reporter.Trace, file);
        }

        foreach (var error in result.Errors)
        {
            reporter.Report(error);
        }

        return result.Plan;
    }
}
