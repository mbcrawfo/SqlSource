using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Linq;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>diagnose</c> command.  With <c>--query</c> it describes one query of a project again, through an exchange
/// that records the session, and writes a bug-report bundle of it: renamed and proved by a replay, with its mapping
/// beside it, or raw under <c>--no-obfuscate</c>.  With <c>--scrub</c> it renames a raw bundle that it wrote, with no
/// database and no project.  <see cref="DiagnoseRun" /> runs both.
/// </summary>
/// <remarks>
/// Its project, connections, <c>.env</c>, <c>--verbose</c> and <c>--log</c> are those of <see cref="SharedOptions" />,
/// which <c>describe</c> takes too, read by the same rules.  <c>--out</c> is required with both forms, and
/// <c>--scrub</c> takes no other option.  A value of <c>--out</c> is held to the rule of <c>--log</c>, so the path it
/// gives is always printed.
/// </remarks>
internal static class DiagnoseCommand
{
    public const string Name = "diagnose";

    internal const string QueryOption = "--query";

    internal const string OutOption = "--out";

    internal const string NoObfuscateOption = "--no-obfuscate";

    internal const string ScrubOption = "--scrub";

    private const string PathArgument = "path";

    /// <summary>
    /// The command as <see cref="Cli" /> runs it: it always reads <c>SQLSOURCE_LOG</c>, writes its errors as they are,
    /// and never ends with <c>Cli.RunAgainLine</c>: it says a refused query on a line of its own, and was asked for one
    /// query.
    /// </summary>
    public static ToolCommand Entry =>
        new()
        {
            Name = Name,
            Create = Create,
            CheckUsage = CheckUsage,
            ReadsValuesAs = ReadsValuesAs,
            ReadsLogVariable = static _ => true,
            WritesOneLine = static _ => false,
            MayPoint = static _ => false,
            Bind = static (parsed, usage, host, reporter, _) => Bind(parsed, usage, host, reporter),
        };

    public static Command Create()
    {
        // The rules of SharedOptions hold, and --project, --connection, --verbose, --log and --env-file are the ones
        // of SharedOptions, with their names and their rules.
        var path = new Argument<string>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one; the current directory when "
                + "left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var project = SharedOptions.ProjectOption();
        var connection = SharedOptions.ConnectionOption();

        var query = new Option<string>(QueryOption)
        {
            Description =
                "The query to report: its .sql file, a colon and its name, as its -- name: line gives it.  The last "
                + "colon ends the file.",
            HelpName = "file:name",
            Arity = ArgumentArity.ExactlyOne,
        };

        var output = new Option<string>(OutOption)
        {
            Description =
                "Where to write the bundle, a zip.  The mapping of its new names is written beside it, with "
                + $"{BundleFiles.MappingSuffix} added to its name.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

        var noObfuscate = new Option<bool>(NoObfuscateOption)
        {
            Description =
                "Write the bundle with your names and SQL as they are, and no mapping.  It must not be posted.",
            Arity = ArgumentArity.Zero,
        };

        var verbose = SharedOptions.VerboseOption(
            "Say what the run decides for the project, the file, the database and the query, as it goes."
        );
        var log = SharedOptions.LogOption();
        var envFile = SharedOptions.EnvFileOption();

        var scrub = new Option<string>(ScrubOption)
        {
            Description =
                $"Rename a raw bundle that {NoObfuscateOption} wrote, with no database and no project, and write it "
                + $"and its mapping at {OutOption}.",
            HelpName = "bundle",
            Arity = ArgumentArity.ExactlyOne,
        };

        return new Command(
            Name,
            "Writes a bug-report bundle of one query: described again, its names renamed, and proved by a replay"
        )
        {
            path,
            project,
            connection,
            query,
            output,
            noObfuscate,
            verbose,
            log,
            envFile,
            scrub,
        };
    }

    /// <summary>
    /// What is wrong with a command line of <c>diagnose</c> by the command's own rules, each a whole line to write.  A
    /// line names an option or a position and never repeats a token.  Empty for another command.
    /// </summary>
    /// <remarks>
    /// The command needs <c>--query</c> or <c>--scrub</c>, and <c>--out</c> with either, unless the help is asked for.
    /// <c>--scrub</c> takes nothing but <c>--out</c>.  A value of <c>--query</c> is a file, a colon and a name, the
    /// colon the last one so that a Windows path reads, and its file is a <c>.sql</c> path.  The argument is the unit,
    /// of which there is one, and never a <c>.sql</c> path, which <c>--query</c> names.  A value of <c>--out</c> or
    /// <c>--log</c> is held to <c>--log</c>'s rule, and one of <c>--env-file</c>, <c>--connection</c>, and an
    /// argument beside one, to the rules of <see cref="SharedOptions" />.
    /// </remarks>
    public static IReadOnlyList<string> CheckUsage(Usage usage)
    {
        if (usage.Command.Name != Name)
        {
            return [];
        }

        var messages = new List<string>();
        var helps = usage.Flags.Any(static flag => flag is HelpOption);
        var queries = SharedOptions.ValuesOf(usage, QueryOption);
        var scrubs = SharedOptions.ValuesOf(usage, ScrubOption);
        if (!helps && queries.IsEmpty && scrubs.IsEmpty)
        {
            messages.Add($"{Cli.LinePrefix}command '{Name}' needs {QueryOption} or {ScrubOption}");
        }

        if (!helps && SharedOptions.ValuesOf(usage, OutOption).IsEmpty)
        {
            messages.Add($"{Cli.LinePrefix}option '{OutOption}' is required");
        }

        if (!scrubs.IsEmpty)
        {
            CheckScrubUsage(usage, queries.IsEmpty, messages);
        }

        var connections = new HashSet<string>(DatabaseName.Comparer);
        var hasConnection = !SharedOptions.ValuesOf(usage, SharedOptions.Connection).IsEmpty;
        foreach (var value in usage.Values)
        {
            if (ProblemOf(value, hasConnection, connections) is { } problem)
            {
                messages.Add(problem);
            }
        }

        return messages;
    }

    // What --scrub takes: --out, and the help.  --query beside it is named as its rival.
    private static void CheckScrubUsage(Usage usage, bool hasNoQuery, List<string> messages)
    {
        if (!hasNoQuery)
        {
            messages.Add($"{Cli.LinePrefix}options '{QueryOption}' and '{ScrubOption}' cannot be used together");
        }

        var others =
            usage.Values.Any(static value => value.Option?.Name is not (ScrubOption or OutOption or QueryOption))
            || usage.Flags.Any(static flag => flag is not HelpOption);
        if (others)
        {
            messages.Add($"{Cli.LinePrefix}option '{ScrubOption}' takes no other option but '{OutOption}'");
        }
    }

    // What is wrong with one value, or null.
    private static string? ProblemOf(UsageValue value, bool hasConnection, HashSet<string> connections) =>
        value.Option?.Name switch
        {
            null when hasConnection && SharedOptions.SplitConnectionOf(value) is { } split => split,
            null when SqlPath.IsSqlFile(value.Text) =>
                $"{Cli.LinePrefix}the argument at position {value.Position} is a .sql path: name the query with "
                    + $"'{QueryOption} <file>:<name>'",
            QueryOption => ProblemOfQuery(value),
            OutOption => SharedOptions.ProblemOfPath(value, OutOption, "a bundle's path", isOnline: false),
            SharedOptions.Log => SharedOptions.ProblemOfLogPath(value, isOnline: false),
            SharedOptions.EnvFile => SharedOptions.ProblemOfEnvFile(value, isOnline: false),
            SharedOptions.Connection => SharedOptions.ProblemOfConnection(value, connections),
            _ => null,
        };

    // What is wrong with a value of --query, or null.
    private static string? ProblemOfQuery(UsageValue value)
    {
        if (QueryName.Of(value) is not { } named)
        {
            return $"{Cli.LinePrefix}the value of option '{QueryOption}' at position {value.Position} "
                + "is not <file>:<name>";
        }

        return SqlPath.IsSqlFile(named.File.Text)
            ? null
            : $"{Cli.LinePrefix}the file of option '{QueryOption}' at position {value.Position} is not a .sql path";
    }

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave <c>--project</c>, <c>--connection</c>,
    /// <c>--log</c>, <c>--env-file</c>, <c>--query</c>, <c>--out</c> and <c>--scrub</c> the same values, in the same
    /// order.  True for a command other than <c>diagnose</c>.
    /// </summary>
    public static bool ReadsValuesAs(Usage usage, ParseResult parsed) =>
        usage.Command.Name != Name
        || (
            SharedOptions.ReadsAs(usage, parsed, SharedOptions.Project)
            && SharedOptions.ReadsAs(usage, parsed, SharedOptions.Connection)
            && SharedOptions.ReadsAsOne(usage, parsed, SharedOptions.Log)
            && SharedOptions.ReadsAsOne(usage, parsed, SharedOptions.EnvFile)
            && SharedOptions.ReadsAsOne(usage, parsed, QueryOption)
            && SharedOptions.ReadsAsOne(usage, parsed, OutOption)
            && SharedOptions.ReadsAsOne(usage, parsed, ScrubOption)
        );

    /// <summary>
    /// Gives the <c>diagnose</c> that a command line runs its action, once the run has its reporter.
    /// </summary>
    /// <param name="parsed">A command line that runs <c>diagnose</c>, by <see cref="SharedOptions.Runs" />.</param>
    /// <param name="usage">The same command line as <see cref="UsageCheck" /> read it, which gives the values.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    public static void Bind(ParseResult parsed, Usage usage, ToolHost host, Reporter reporter) =>
        parsed.CommandResult.Command.SetAction(
            async (command, cancellationToken) =>
            {
                // CheckUsage has held the command line to one of the two forms, each with its --out.
                var output = SharedOptions.ValuesOf(usage, OutOption).Single();
                if (SharedOptions.ValuesOf(usage, ScrubOption) is [{ } scrub])
                {
                    await DiagnoseRun.ScrubAsync(scrub, output, host, reporter, cancellationToken);
                }
                else
                {
                    var query =
                        QueryName.Of(SharedOptions.ValuesOf(usage, QueryOption).Single())
                        ?? throw new InvalidOperationException("A --query that is no file and name was let through.");
                    await DiagnoseRun.QueryAsync(
                        usage,
                        query,
                        output,
                        command.GetValue<bool>(NoObfuscateOption),
                        host,
                        reporter,
                        cancellationToken
                    );
                }

                // An error is in the reporter, where the exit code is taken.
                return 0;
            }
        );

    /// <summary>
    /// A value of <c>--query</c> as the run reads it: the file, before the last colon, and the query's name after it.
    /// </summary>
    /// <param name="File">
    /// The file, as a value of the option at its position, so that a message names it as a path of the command line.
    /// </param>
    /// <param name="Query">The query's name, which a message prints.</param>
    internal sealed record QueryName(UsageValue File, string Query)
    {
        /// <summary>The file and the name of a value, or null when either is missing or blank.</summary>
        public static QueryName? Of(UsageValue value)
        {
            var colon = value.Text.LastIndexOf(':');
            if (colon < 0)
            {
                return null;
            }

            var file = value.Text[..colon];
            var name = value.Text[(colon + 1)..];
            return string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(name)
                ? null
                : new QueryName(value with { Text = file }, name);
        }
    }
}
