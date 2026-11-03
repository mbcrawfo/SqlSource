using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
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
/// The <c>describe</c> command.  It finds the unit and the projects, builds the plan of the run and reports what is
/// wrong with it, and hands the plan to <see cref="DescribeRun" />, which describes, and writes or, under
/// <c>--check</c>, compares.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    internal const string ProjectOption = "--project";

    internal const string DatabaseOption = "--database";

    internal const string ConnectionOption = "--connection";

    private const string ForceOption = "--force";

    private const string CheckOption = "--check";

    private const string VerboseOption = "--verbose";

    internal const string LogOption = "--log";

    internal const string ManifestOption = "--manifest";

    internal const string OnlineOption = "--online";

    private const string OnlineAuto = "auto";

    private const string OnlineTrue = "true";
    internal const string WatchOption = "--watch";

    /// <summary>The environment variable that gives the log's path when <c>--log</c> does not.</summary>
    internal const string LogVariable = "SQLSOURCE_LOG";

    public static Command Create()
    {
        // Every option is declared so that UsageCheck reads it right.  A flag has Arity = ArgumentArity.Zero:
        // System.CommandLine's default for a bool takes an optional value, and the token after the flag would be read
        // as that value.  An option that takes a value takes exactly one each time it is given: ExactlyOne when it is
        // given once, and an array with OneOrMore when it may be repeated, since System.CommandLine rejects a second
        // ExactlyOne.  UsageCheck rejects that second one itself, since System.CommandLine's message repeats the
        // value.  An option is added to --help in the change that gives it an effect, not before: one that is read
        // and checked earlier is Hidden until then, as --database was in sub-phase 2.4.

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
                    if (FindUnit(usage, host, reporter) is { } watched)
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
                if (await PlanUnitAsync(usage, host, reporter, cancellationToken) is { } planned)
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
            messages.Add($"sqlsource: options '{WatchOption}' and '{CheckOption}' cannot be used together");
        }

        var units = 0;
        var connections = new HashSet<string>(DatabaseName.Comparer);
        var hasConnection = usage.Values.Any(static value => value.Option?.Name == ConnectionOption);
        var isOnline = IsOnline(usage);
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
            else if (value.Option.Name == LogOption && !isOnline && !value.MayBePrintedAsPath)
            {
                // "=" and ";" are what a connection string is made of: the value may be one typed after --log.  The
                // build's paths may hold them, as a project's name may, and a message names such a path by its
                // position.
                messages.Add(
                    $"sqlsource: the value of option '{LogOption}' at position {value.Position} holds '=' or ';', "
                        + "which a log's path may not"
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

        CheckManifestUsage(usage, messages);
        return messages;
    }

    // The rules of --manifest and --online.  A run from a manifest is on the one project the manifest names, so
    // nothing else names what to run, and --online is the build's word about that run.
    private static void CheckManifestUsage(Usage usage, List<string> messages)
    {
        var manifests = ValuesOf(usage, ManifestOption);
        var isOnline = IsOnline(usage);
        messages.AddRange(manifests.Select(manifest => ProblemOfManifestPath(manifest, isOnline)).OfType<string>());

        if (manifests.Length > 0)
        {
            messages.AddRange(
                usage
                    .Values.Select(static value => (value.Position, Kind: WhatIsNamedBy(value)))
                    .Where(static named => named.Kind is not null)
                    .Select(named =>
                        $"sqlsource: option '{ManifestOption}' takes no {named.Kind} (position {named.Position})"
                    )
            );
        }

        var onlines = ValuesOf(usage, OnlineOption);
        messages.AddRange(
            onlines
                .Where(static online => OnlineModeOf(online.Text) is null)
                .Select(online =>
                    $"sqlsource: the value of option '{OnlineOption}' at position {online.Position} is not "
                    + $"'{OnlineAuto}' or '{OnlineTrue}'"
                )
        );

        if (manifests.Length == 0 && onlines.Length > 0)
        {
            messages.Add($"sqlsource: option '{OnlineOption}' needs '{ManifestOption}'");
        }
    }

    // Whether the command line has --online: the build's, whose paths of --manifest and --log are its own.
    private static bool IsOnline(Usage usage) => usage.Values.Any(static value => value.Option?.Name == OnlineOption);

    // What --online said, as OnlineOf(ParseResult) reads it, for the steps that have only the usage, a watch's passes
    // among them.  A watch is never online: --online needs --manifest, which takes no --watch.
    private static OnlineMode OnlineOf(Usage usage) =>
        ValuesOf(usage, OnlineOption) is [{ } online]
            ? OnlineModeOf(online.Text)
                ?? throw new InvalidOperationException("An --online that is no mode was let through.")
            : OnlineMode.Off;

    // What is wrong with the value of --manifest as a path, as a whole line, or null.  Without --online a path that
    // holds "=" or ";" is refused, as one of --log is: it may be a connection string typed after the option.
    private static string? ProblemOfManifestPath(UsageValue manifest, bool isOnline)
    {
        if (string.IsNullOrWhiteSpace(manifest.Text))
        {
            return $"sqlsource: the value of option '{ManifestOption}' at position {manifest.Position} is not a path";
        }

        return !isOnline && !manifest.MayBePrintedAsPath
            ? $"sqlsource: the value of option '{ManifestOption}' at position {manifest.Position} holds '=' or ';', "
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
            ProjectOption => $"'{ProjectOption}'",
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
    /// Whether the command line runs <c>describe</c>: the command is named, and it is its own action that runs
    /// and not the help's.  Until <see cref="Bind" /> gives the command its action it has none, and neither has a
    /// command line that runs it, so the answer is the same before and after.
    /// </summary>
    public static bool Runs(ParseResult parsed) =>
        parsed.CommandResult.Command.Name == Name
        && ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action);

    /// <summary>Whether a <c>describe</c> that runs was given <c>--verbose</c>.</summary>
    public static bool IsVerbose(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(VerboseOption);

    /// <summary>Whether a <c>describe</c> that runs was given <c>--watch</c>.</summary>
    public static bool Watches(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(WatchOption);

    /// <summary>
    /// The path of the run's log as it was given, by <c>--log</c> or else by <c>SQLSOURCE_LOG</c>, or null when
    /// the run has none.  A command line that does not run <c>describe</c> has none: the help must not empty the
    /// log of the last run.  A variable that is empty or white space is not set, and a run under <c>--online</c>
    /// does not read it.
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

        // The build passes --log, with a path of its own for each project and framework, when SQLSOURCE_LOG asks for
        // logs.  Read here too, the projects built at once would open one file.
        if (OnlineOf(parsed) != OnlineMode.Off)
        {
            return null;
        }

        var variable = getEnvironmentVariable(LogVariable);
        return string.IsNullOrWhiteSpace(variable) ? null : variable;
    }

    /// <summary>
    /// The value of <c>--log</c> as <see cref="UsageCheck" /> read it, which names the path in a message, or null when
    /// the option was not given.
    /// </summary>
    public static UsageValue? LogOptionOf(Usage usage) => ValuesOf(usage, LogOption) is [{ } value] ? value : null;

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave <c>--project</c>, <c>--database</c>,
    /// <c>--connection</c> and <c>--log</c> the same values, in the same order.  True for a command other than
    /// <c>describe</c>.
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
            ReadsAs(usage, parsed, ProjectOption)
            && ReadsAs(usage, parsed, DatabaseOption)
            && ReadsAs(usage, parsed, ConnectionOption)
            && ValuesOf(usage, LogOption)
                .Select(static value => value.Text)
                .SequenceEqual(parsed.GetValue<string>(LogOption) is { } log ? [log] : [], StringComparer.Ordinal)
            && ReadsAsOne(usage, parsed, ManifestOption)
            && ReadsAsOne(usage, parsed, OnlineOption)
        );

    private static bool ReadsAs(Usage usage, ParseResult parsed, string option) =>
        ValuesOf(usage, option)
            .Select(static value => value.Text)
            .SequenceEqual(parsed.GetValue<string[]>(option) ?? [], StringComparer.Ordinal);

    // For an option that takes one value, given once.
    private static bool ReadsAsOne(Usage usage, ParseResult parsed, string option) =>
        ValuesOf(usage, option)
            .Select(static value => value.Text)
            .SequenceEqual(parsed.GetValue<string>(option) is { } text ? [text] : [], StringComparer.Ordinal);

    // The values of an option, in order.
    private static ImmutableArray<UsageValue> ValuesOf(Usage usage, string option) =>
        [.. usage.Values.Where(value => value.Option?.Name == option)];

    // The arguments of the command: the unit, and the .sql paths.
    private static ImmutableArray<UsageValue> ArgumentsOf(Usage usage) =>
        [.. usage.Values.Where(static value => value.Option is null)];

    // What the command line says of the run, beside its plan.  CheckUsage has held each --connection to its form.
    // The plan's filters hold --database and the .sql paths, and not --project.  A .sql path that names no file still
    // counts as a filter, so that such a run deletes no sidecar.
    private static DescribeOptions OptionsOf(Usage usage, RunUnit unit, RunPlan plan, RunMode mode)
    {
        var connections = ImmutableArray.CreateBuilder<ConnectionArgument>();
        foreach (var value in ValuesOf(usage, ConnectionOption))
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

        return new DescribeOptions(
            mode,
            ValuesOf(usage, ProjectOption).Length > 0 || !plan.Filters.Files.IsEmpty || !plan.Filters.Databases.IsEmpty,
            connections.ToImmutable(),
            plan.Filters.Databases,
            Path.GetDirectoryName(unit.Path) ?? throw new InvalidOperationException("A unit is a file in a folder."),
            OnlineOf(usage)
        );
    }

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
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project of it that uses SqlSource and could be read.
    /// </summary>
    /// <remarks>
    /// The summary of a run prints counts for each selected database and not which queries it selected, so a test
    /// reads the plan from here, and the run goes on from it.
    /// </remarks>
    internal static async Task<RunPlan?> PlanAsync(
        Usage usage,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    ) => (await PlanUnitAsync(usage, host, reporter, cancellationToken))?.Plan;

    // The plan, with the unit it was built for, whose folder is where the run's .env is searched for.
    private static async Task<(RunUnit Unit, RunPlan Plan)?> PlanUnitAsync(
        Usage usage,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        RunUnit unit;
        ImmutableArray<ProjectManifest> manifests;
        if (ValuesOf(usage, ManifestOption) is [{ } manifestValue])
        {
            // The build has evaluated the project, so MSBuild is not asked again, and the project is the unit.
            manifests = await RunProjects.FromManifestAsync(
                manifestValue,
                Path.GetFullPath(manifestValue.Text, host.WorkingDirectory),
                host,
                reporter,
                cancellationToken
            );
            if (manifests.IsEmpty)
            {
                return null;
            }

            unit = new RunUnit(RunUnitKind.Project, manifests[0].ProjectPath);
            RunEvents.Unit(reporter.Trace, unit, wasGiven: true);
        }
        else if (FindUnit(usage, host, reporter) is { } found)
        {
            unit = found;
            manifests = (await FindManifestsAsync(unit, usage, host, reporter, cancellationToken)).Manifests;
        }
        else
        {
            return null;
        }

        return await PlanFromAsync(unit, manifests, usage, host, reporter, cancellationToken) is { } plan
            ? (unit, plan)
            : null;
    }

    /// <summary>
    /// The unit that a command line runs on, from its path or from the working directory.  Null when there is none,
    /// which is reported.
    /// </summary>
    internal static RunUnit? FindUnit(Usage usage, ToolHost host, Reporter reporter)
    {
        // With file filters and no unit, the unit is found from the working directory, not from the files.
        var unitPath = ArgumentsOf(usage).FirstOrDefault(static path => !SqlPath.IsSqlFile(path.Text));
        var found = RunUnitFinder.Find(unitPath, host.WorkingDirectory);
        if (!found.Succeeded)
        {
            reporter.Report(found.Failure);
            return null;
        }

        RunEvents.Unit(reporter.Trace, found.Value, wasGiven: unitPath is not null);
        return found.Value;
    }

    /// <summary>
    /// The projects of the unit that the command line runs, which <c>--project</c> narrows, and their manifests: the
    /// evaluation of each, and its manifest target.  A project with an error is reported and has no manifest.
    /// </summary>
    internal static Task<FoundProjects> FindManifestsAsync(
        RunUnit unit,
        Usage usage,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    ) => RunProjects.FindAsync(unit, ValuesOf(usage, ProjectOption), host, reporter, cancellationToken);

    /// <summary>
    /// The plan of the manifests, which <c>--database</c> and the <c>.sql</c> paths narrow, with what is wrong with it
    /// reported.  Null when there is no manifest; a run with no error then says that no project uses SqlSource.
    /// </summary>
    internal static async Task<RunPlan?> PlanFromAsync(
        RunUnit unit,
        ImmutableArray<ProjectManifest> manifests,
        Usage usage,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
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
            ArgumentsOf(usage).Where(static path => SqlPath.IsSqlFile(path.Text)),
            ValuesOf(usage, DatabaseOption),
            host.WorkingDirectory
        );
        var result = RunPlanner.Plan(manifests, filters, cancellationToken);
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
        // code of 1 with no error line would be a tool that failed.
        var isOnline = IsOnline(usage);
        foreach (var error in result.Errors)
        {
            if (isOnline && IsReportedByTheCompiler(error))
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
    ) => DescribeRun.RunAsync(plan, OptionsOf(usage, unit, plan, mode), host, reporter, counted, cancellationToken);
}
