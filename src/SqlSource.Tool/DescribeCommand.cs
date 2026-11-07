using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Generation.Paths;
using SqlSource.Settings;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Watching;

namespace SqlSource.Tool;

/// <summary>
/// The <c>describe</c> command.  Its run takes the plan from <see cref="PlanSteps" />, which finds the unit and the
/// projects, builds the plan and reports what is wrong with it, and hands it to <see cref="DescribeRun" />, which
/// describes, and writes or, under <c>--check</c>, compares.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    internal const string DatabaseOption = "--database";

    private const string ForceOption = "--force";

    private const string CheckOption = "--check";

    internal const string ManifestOption = "--manifest";

    internal const string OnlineOption = "--online";

    private const string OnlineAuto = "auto";

    private const string OnlineTrue = "true";
    internal const string WatchOption = "--watch";

    /// <summary>
    /// The command as <see cref="Cli" /> runs it.  Under <c>--online</c> it reads no <c>SQLSOURCE_LOG</c>, writes
    /// each error on one line and never ends with <c>Cli.RunAgainLine</c>, and a watch never ends with it either.
    /// </summary>
    public static ToolCommand Entry =>
        new()
        {
            Name = Name,
            Create = Create,
            CheckUsage = CheckUsage,
            ReadsValuesAs = ReadsValuesAs,
            ReadsLogVariable = static parsed => OnlineOf(parsed) == OnlineMode.Off,
            WritesOneLine = static parsed => OnlineOf(parsed) != OnlineMode.Off,
            MayPoint = static parsed => OnlineOf(parsed) == OnlineMode.Off && !Watches(parsed),
            Bind = Bind,
        };

    public static Command Create()
    {
        // The rules of SharedOptions hold: every option is declared so that UsageCheck reads it right.

        // Several, so that System.CommandLine takes a .sql path beside the unit.  CheckUsage holds them to one unit.
        var path = new Argument<string[]>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one; the current directory when "
                + "left out.  A path that ends in .sql restricts the run to that file, and may be given several times.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var project = SharedOptions.ProjectOption();

        // It selects queries: what is described, what is written, and what the summary counts.
        var database = new Option<string[]>(DatabaseOption)
        {
            Description = "A database to describe the queries of.  May be given several times.",
            HelpName = "name",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var connection = SharedOptions.ConnectionOption();

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

        var verbose = SharedOptions.VerboseOption(
            "Say what the run decides for each project, file, database and query, as it goes."
        );
        var log = SharedOptions.LogOption();
        var envFile = SharedOptions.EnvFileOption();

        // The two options of the build that calls the tool.  Each takes exactly one value, given once.
        var manifest = new Option<string>(ManifestOption)
        {
            Description =
                "Describe the project that this manifest file describes, without asking MSBuild.  For the use of "
                + "the SqlSource build, which has written the file.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

        var online = new Option<string>(OnlineOption)
        {
            Description =
                "How the build asked for the databases: 'auto' says the run is offline, and fails nothing, when no "
                + "database with a query to describe has a connection; 'true' makes a missing connection an error.  "
                + "Needs --manifest.  For the use of the SqlSource build.",
            HelpName = "auto|true",
            Arity = ArgumentArity.ExactlyOne,
        };

        var watch = new Option<bool>(WatchOption)
        {
            Description = "Describe again each time a file of the run changes, until stopped with Ctrl+C.",
            Arity = ArgumentArity.Zero,
        };

        return new Command(Name, "Asks the databases of the projects to describe their queries")
        {
            path,
            project,
            database,
            connection,
            force,
            check,
            verbose,
            log,
            envFile,
            manifest,
            online,
            watch,
        };
    }

    /// <summary>
    /// Gives the <c>describe</c> that a command line runs its action, once the run has its reporter.
    /// System.CommandLine takes a command's action when it invokes, not when it parses.
    /// </summary>
    /// <param name="parsed">A command line for which <see cref="Runs" /> is true.</param>
    /// <param name="usage">The same command line as <see cref="UsageCheck" /> read it, which gives the values.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="counted">Given the counts of each selected database, as <c>run.end</c> holds them.</param>
    public static void Bind(
        ParseResult parsed,
        Usage usage,
        ToolHost host,
        Reporter reporter,
        Action<LogValue> counted
    ) =>
        parsed.CommandResult.Command.SetAction(
            async (command, cancellationToken) =>
            {
                // A watch runs a pass after each change, until it is stopped, and ends only by its token.  A unit that
                // cannot be found ends it before the first pass.
                if (Watches(command))
                {
                    if (PlanSteps.FindUnit(usage, host, reporter) is { } watched)
                    {
                        await WatchSession.RunAsync(
                            watched,
                            usage,
                            command,
                            host,
                            reporter,
                            counted,
                            cancellationToken
                        );
                    }

                    return 0;
                }

                // An error is in the reporter, where the exit code is taken.
                if (await PlanSteps.PlanUnitAsync(usage, host, reporter, cancellationToken) is { } planned)
                {
                    await DescribePlanAsync(
                        planned.Plan,
                        usage,
                        planned.Unit,
                        ModeOf(command),
                        host,
                        reporter,
                        counted,
                        cancellationToken
                    );
                }

                return 0;
            }
        );

    /// <summary>
    /// What is wrong with a command line of <c>describe</c> by the command's own rules, each a whole line to write.
    /// A line names a position and never repeats a token.  Empty for another command.
    /// </summary>
    /// <remarks>
    /// A path that ends in <c>.sql</c> is a filter and any other is the unit, of which there is one.  A value of
    /// <c>--database</c> is a database name, by the rule of the <c>-- database:</c> marker.  A value of
    /// <c>--connection</c> is a name, <c>=</c> and a value, and a name is given once.  On a line with a
    /// <c>--connection</c>, an argument that holds <c>=</c> or <c>;</c>, a <c>.sql</c> path too, is taken for the rest
    /// of a value that the shell split: it would be printed as a path, a <c>.sql</c> one in full by <c>SQLSRC212</c>.
    /// A path may hold either, as a folder of <c>tests/SqlSource.Tool.Tests/Fixtures/Projects/OddPaths</c> does, so a
    /// line with no <c>--connection</c> is left as it is.  <c>Cli.ReadCommandLineAsync</c> calls this only when
    /// <see cref="UsageCheck" /> found nothing.
    /// </remarks>
    public static IReadOnlyList<string> CheckUsage(Usage usage)
    {
        if (usage.Command.Name != Name)
        {
            return [];
        }

        var messages = new List<string>();

        // A watch writes the sidecars again each time, and a check writes none.
        if (
            usage.Flags.Any(static flag => flag.Name == WatchOption)
            && usage.Flags.Any(static flag => flag.Name == CheckOption)
        )
        {
            messages.Add($"{Cli.LinePrefix}options '{WatchOption}' and '{CheckOption}' cannot be used together");
        }

        var units = 0;
        var connections = new HashSet<string>(DatabaseName.Comparer);
        var hasConnection = usage.Values.Any(static value => value.Option?.Name == SharedOptions.Connection);
        var isOnline = IsOnline(usage);
        foreach (var value in usage.Values)
        {
            if (value.Option is null)
            {
                if (hasConnection && SharedOptions.SplitConnectionOf(value) is { } split)
                {
                    messages.Add(split);
                }
                else if (!SqlPath.IsSqlFile(value.Text) && ++units > 1)
                {
                    messages.Add($"{Cli.LinePrefix}unexpected argument at position {value.Position}");
                }
            }
            else if (value.Option.Name == DatabaseOption && !SettingValue.IsDatabaseName(value.Text))
            {
                messages.Add(
                    $"{Cli.LinePrefix}the value of option '{DatabaseOption}' at position {value.Position} is not a "
                        + "database name"
                );
            }
            else if (value.Option.Name == SharedOptions.Log)
            {
                if (SharedOptions.ProblemOfLogPath(value, isOnline) is { } problem)
                {
                    messages.Add(problem);
                }
            }
            else if (value.Option.Name == SharedOptions.EnvFile)
            {
                if (SharedOptions.ProblemOfEnvFile(value, isOnline) is { } problem)
                {
                    messages.Add(problem);
                }
            }
            else if (
                value.Option.Name == SharedOptions.Connection
                && SharedOptions.ProblemOfConnection(value, connections) is { } problem
            )
            {
                messages.Add(problem);
            }
        }

        CheckManifestUsage(usage, messages);
        return messages;
    }

    // The rules of --manifest and --online.  A run from a manifest is on the one project the manifest names, so
    // nothing else names what to run, and --online is the build's word about that run.
    private static void CheckManifestUsage(Usage usage, List<string> messages)
    {
        var manifests = SharedOptions.ValuesOf(usage, ManifestOption);
        var isOnline = IsOnline(usage);
        messages.AddRange(manifests.Select(manifest => ProblemOfManifestPath(manifest, isOnline)).OfType<string>());

        if (manifests.Length > 0)
        {
            messages.AddRange(
                usage
                    .Values.Select(static value => (value.Position, Kind: WhatIsNamedBy(value)))
                    .Where(static named => named.Kind is not null)
                    .Select(named =>
                        $"{Cli.LinePrefix}option '{ManifestOption}' takes no {named.Kind} (position {named.Position})"
                    )
            );

            // A watch evaluates the projects again after a change, and a run from a manifest evaluates none.
            if (usage.Flags.Any(static flag => flag.Name == WatchOption))
            {
                messages.Add($"{Cli.LinePrefix}option '{ManifestOption}' takes no '{WatchOption}'");
            }
        }

        var onlines = SharedOptions.ValuesOf(usage, OnlineOption);
        messages.AddRange(
            onlines
                .Where(static online => OnlineModeOf(online.Text) is null)
                .Select(online =>
                    $"{Cli.LinePrefix}the value of option '{OnlineOption}' at position {online.Position} is not "
                    + $"'{OnlineAuto}' or '{OnlineTrue}'"
                )
        );

        if (manifests.Length == 0 && onlines.Length > 0)
        {
            messages.Add($"{Cli.LinePrefix}option '{OnlineOption}' needs '{ManifestOption}'");
        }
    }

    /// <summary>
    /// Whether the command line has <c>--online</c>: the build's, whose paths of <c>--manifest</c>, <c>--log</c> and
    /// <c>--env-file</c> are its own.
    /// </summary>
    internal static bool IsOnline(Usage usage) => usage.Values.Any(static value => value.Option?.Name == OnlineOption);

    // What --online said, as OnlineOf(ParseResult) reads it, for the steps that have only the usage, a watch's passes
    // among them.  A watch is never online: --online needs --manifest, which takes no --watch.
    private static OnlineMode OnlineOf(Usage usage) =>
        SharedOptions.ValuesOf(usage, OnlineOption) is [{ } online]
            ? OnlineModeOf(online.Text)
                ?? throw new InvalidOperationException("An --online that is no mode was let through.")
            : OnlineMode.Off;

    // What is wrong with the value of --manifest as a path, as a whole line, or null.  Without --online a path that
    // holds "=" or ";" is refused, as one of --log is: it may be a connection string typed after the option.
    private static string? ProblemOfManifestPath(UsageValue manifest, bool isOnline)
    {
        if (string.IsNullOrWhiteSpace(manifest.Text))
        {
            return $"{Cli.LinePrefix}the value of option '{ManifestOption}' at position {manifest.Position} "
                + "is not a path";
        }

        return !isOnline && !manifest.MayBePrintedAsPath
            ? $"{Cli.LinePrefix}the value of option '{ManifestOption}' at position {manifest.Position} holds "
                + "'=' or ';', "
                + "which a manifest's path may not"
            : null;
    }

    // The mode that a value of --online names, ignoring case, or null when it names none.
    private static OnlineMode? OnlineModeOf(string text)
    {
        if (string.Equals(text, OnlineAuto, StringComparison.OrdinalIgnoreCase))
        {
            return OnlineMode.Auto;
        }

        return string.Equals(text, OnlineTrue, StringComparison.OrdinalIgnoreCase) ? OnlineMode.True : null;
    }

    // What a value names of the run, which a run from a manifest does not take: the unit, a .sql file, a project or a
    // database.  Null for the rest.
    private static string? WhatIsNamedBy(UsageValue value) =>
        value.Option?.Name switch
        {
            null => SqlPath.IsSqlFile(value.Text) ? ".sql path" : "path",
            SharedOptions.Project => $"'{SharedOptions.Project}'",
            DatabaseOption => $"'{DatabaseOption}'",
            _ => null,
        };

    /// <summary>
    /// What <c>--online</c> said of a <c>describe</c> that runs: <see cref="OnlineMode.Off" /> when it was not given
    /// or the command line does not run <c>describe</c>.  <see cref="CheckUsage" /> has held the value to the two
    /// words.
    /// </summary>
    public static OnlineMode OnlineOf(ParseResult parsed) =>
        Runs(parsed) && parsed.GetValue<string>(OnlineOption) is { } text
            ? OnlineModeOf(text) ?? throw new InvalidOperationException("An --online that is no mode was let through.")
            : OnlineMode.Off;

    /// <summary>
    /// Whether the command line runs <c>describe</c>, and not its help, by <see cref="SharedOptions.Runs" />.
    /// </summary>
    public static bool Runs(ParseResult parsed) => SharedOptions.Runs(parsed, Name);

    /// <summary>Whether a <c>describe</c> that runs was given <c>--watch</c>.</summary>
    public static bool Watches(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(WatchOption);

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave <c>--project</c>, <c>--database</c>,
    /// <c>--connection</c>, <c>--log</c>, <c>--env-file</c>, <c>--manifest</c> and <c>--online</c> the same values, in
    /// the same order.  True for a command other than <c>describe</c>.
    /// </summary>
    /// <remarks>
    /// The run reads these values from <see cref="Usage" />, and the log's <c>arguments</c> is redacted from it,
    /// while System.CommandLine gives the log's path and the flags.  If the two disagree on
    /// which token is the value of an option, a token could be printed where the other reader would not print it,
    /// so <c>Cli</c> runs nothing then.
    /// </remarks>
    public static bool ReadsValuesAs(Usage usage, ParseResult parsed) =>
        usage.Command.Name != Name
        || (
            SharedOptions.ReadsAs(usage, parsed, SharedOptions.Project)
            && SharedOptions.ReadsAs(usage, parsed, DatabaseOption)
            && SharedOptions.ReadsAs(usage, parsed, SharedOptions.Connection)
            && SharedOptions.ReadsAsOne(usage, parsed, SharedOptions.Log)
            && SharedOptions.ReadsAsOne(usage, parsed, SharedOptions.EnvFile)
            && SharedOptions.ReadsAsOne(usage, parsed, ManifestOption)
            && SharedOptions.ReadsAsOne(usage, parsed, OnlineOption)
        );

    // What the command line says of the run, beside its plan.  CheckUsage has held each --connection to its form.
    // The plan's filters hold --database and the .sql paths, and not --project.  A .sql path that names no file still
    // counts as a filter, so that such a run deletes no sidecar.
    private static DescribeOptions OptionsOf(Usage usage, RunUnit unit, RunPlan plan, RunMode mode, ToolHost host) =>
        new(
            mode,
            SharedOptions.ValuesOf(usage, SharedOptions.Project).Length > 0
                || !plan.Filters.Files.IsEmpty
                || !plan.Filters.Databases.IsEmpty,
            PlanSteps.ConnectionsOf(usage),
            plan.Filters.Databases,
            PlanSteps.UnitFolderOf(unit),
            PlanSteps.EnvFileOf(usage, PlanSteps.UnitFolderOf(unit), host),
            OnlineOf(usage)
        );

    /// <summary>
    /// The mode that the command line asks for.  <c>--check</c> beside <c>--force</c> is <c>--check</c>: it describes
    /// every query, as <c>--force</c> does, and writes nothing.
    /// </summary>
    internal static RunMode ModeOf(ParseResult parsed) =>
        (parsed.GetValue<bool>(CheckOption), parsed.GetValue<bool>(ForceOption)) switch
        {
            (true, _) => RunMode.Check,
            (false, true) => RunMode.Force,
            _ => RunMode.Describe,
        };

    /// <summary>
    /// Describes the plan, and writes or deletes the sidecars, in the mode given: the steps after the plan, with what
    /// the command line says of the run beside it.
    /// </summary>
    internal static Task DescribePlanAsync(
        RunPlan plan,
        Usage usage,
        RunUnit unit,
        RunMode mode,
        ToolHost host,
        Reporter reporter,
        Action<LogValue> counted,
        CancellationToken cancellationToken
    ) =>
        DescribeRun.RunAsync(
            plan,
            OptionsOf(usage, unit, plan, mode, host),
            host,
            reporter,
            counted,
            cancellationToken
        );
}
