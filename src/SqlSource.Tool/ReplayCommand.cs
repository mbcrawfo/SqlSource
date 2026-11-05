using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>replay</c> command.  It reads a bug-report bundle, a zip or a folder, runs its one query through the
/// pipeline with the describer answered from the bundle and no database, and says whether that gives the bundle's
/// answer.
/// </summary>
/// <remarks>
/// <para>
/// It writes on standard output, in order: that the bundle is not renamed, when its <c>bundle.json</c> says so; a line
/// for each decision of the walk and of the table match, or under <c>--verbose</c> for every event of the describer
/// (<see cref="ReplayLines" />); then the outcome.  The outcome is that the answer is reproduced, exit code <c>0</c>;
/// that it is not, with each difference, exit code <c>2</c>; or, for a bundle that holds no answer, what the replay
/// gave, exit code <c>0</c>.  A bundle that cannot be read or replayed is <c>SQLSRC288</c> to <c>SQLSRC291</c>, and
/// exit code <c>1</c>.
/// </para>
/// <para>
/// An error is at the bundle's full path.  When the token of the path holds <c>=</c> or <c>;</c>, the error is at no
/// file, a line under it names the path by its position, and every text of the path in its message, as a reason of
/// the system quotes it, is replaced by that position.
/// </para>
/// </remarks>
internal static class ReplayCommand
{
    public const string Name = "replay";

    private const string BundleArgument = "bundle";

    private const string VerboseOption = "--verbose";

    private const string Prefix = "sqlsource: ";

    private static readonly JsonSerializerOptions EntryOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Command Create()
    {
        // The rules of DescribeCommand.Create hold: a flag has Arity = ArgumentArity.Zero, and an option that takes a
        // value takes exactly one.  --log is describe's, with its name, its variable and its rules.
        var bundle = new Argument<string>(BundleArgument)
        {
            Description = "A bundle: the zip, or the folder it was unzipped to.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var verbose = new Option<bool>(VerboseOption)
        {
            Description = "Print every event of the replay, not only the decisions of the walk and the table match.",
            Arity = ArgumentArity.Zero,
        };

        var log = new Option<string>(DescribeCommand.LogOption)
        {
            Description =
                "Write what the run did to this file, one JSON object on a line.  The file is overwritten.  "
                + $"{DescribeCommand.LogVariable} gives the path when the option does not.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

        return new Command(
            Name,
            "Runs the pipeline from a bug-report bundle with no database, and says whether it gives the bundle's answer"
        )
        {
            bundle,
            verbose,
            log,
        };
    }

    /// <summary>
    /// Whether the command line runs <c>replay</c>: the command is named, and it is its own action that runs and not
    /// the help's, as <see cref="DescribeCommand.Runs" /> says it of <c>describe</c>.
    /// </summary>
    public static bool Runs(ParseResult parsed) =>
        parsed.CommandResult.Command.Name == Name
        && ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action);

    /// <summary>Whether a <c>replay</c> that runs was given <c>--verbose</c>.</summary>
    public static bool IsVerbose(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(VerboseOption);

    /// <summary>
    /// The path of the run's log as it was given, by <c>--log</c> or else by <c>SQLSOURCE_LOG</c>, or null when the
    /// run has none, as <see cref="DescribeCommand.LogPathOf" /> reads it for <c>describe</c>.
    /// </summary>
    public static string? LogPathOf(ParseResult parsed, Func<string, string?> getEnvironmentVariable)
    {
        if (!Runs(parsed))
        {
            return null;
        }

        if (parsed.GetValue<string>(DescribeCommand.LogOption) is { } option)
        {
            return option;
        }

        var variable = getEnvironmentVariable(DescribeCommand.LogVariable);
        return string.IsNullOrWhiteSpace(variable) ? null : variable;
    }

    /// <summary>
    /// The value of <c>--log</c> as <see cref="UsageCheck" /> read it, or null when it was not given.
    /// </summary>
    public static UsageValue? LogOptionOf(Usage usage) => LogsOf(usage) is [{ } value] ? value : null;

    /// <summary>
    /// What is wrong with a command line of <c>replay</c> by the command's own rules, each a whole line to write.
    /// Empty for another command.
    /// </summary>
    /// <remarks>
    /// System.CommandLine needs the bundle and says so with a message of its own, so the line that asks for it is
    /// written here, unless the help is asked for.  A value of <c>--log</c> is held to <c>describe</c>'s rule.
    /// </remarks>
    public static IReadOnlyList<string> CheckUsage(Usage usage)
    {
        if (usage.Command.Name != Name)
        {
            return [];
        }

        var messages = new List<string>();
        if (usage.Arguments.Count == 0 && !usage.Flags.Any(static flag => flag is HelpOption))
        {
            messages.Add($"sqlsource: command '{Name}' needs the path of a {BundleArgument}");
        }

        messages.AddRange(
            LogsOf(usage).Select(static log => DescribeCommand.ProblemOfLogPath(log, isOnline: false)).OfType<string>()
        );
        return messages;
    }

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave <c>--log</c> the same value.  True for a command
    /// other than <c>replay</c>.
    /// </summary>
    public static bool ReadsValuesAs(Usage usage, ParseResult parsed) =>
        usage.Command.Name != Name
        || LogsOf(usage)
            .Select(static value => value.Text)
            .SequenceEqual(
                parsed.GetValue<string>(DescribeCommand.LogOption) is { } log ? [log] : [],
                StringComparer.Ordinal
            );

    /// <summary>
    /// Gives the <c>replay</c> that a command line runs its action, once the run has its reporter.
    /// </summary>
    /// <param name="parsed">A command line for which <see cref="Runs" /> is true.</param>
    /// <param name="usage">The same command line as <see cref="UsageCheck" /> read it, which gives the bundle.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">
    /// Where the errors go, with the run's trace, whose log is given the describer's events.
    /// </param>
    public static void Bind(ParseResult parsed, Usage usage, ToolHost host, Reporter reporter) =>
        parsed.CommandResult.Command.SetAction(
            (command, cancellationToken) =>
                ReplayAsync(
                    usage.Values.Single(static value => value.Option is null),
                    IsVerbose(command),
                    host,
                    reporter,
                    cancellationToken
                )
        );

    // The replay of the bundle at the argument.  An error is in the reporter, where the exit code is taken; the
    // command gives 2 for an answer that is not the bundle's.
    private static async Task<int> ReplayAsync(
        UsageValue argument,
        bool verbose,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        var path = Path.GetFullPath(argument.Text, host.WorkingDirectory);
        void Report(ToolDiagnostic error) => reporter.Report(AsNamed(error, argument, path));

        var read = BundleReader.Read(path, host.OpenReadFile, host.ReadTextFile);
        if (!read.Succeeded)
        {
            Report(read.Failure);
            return 0;
        }

        var bundle = read.Value;
        if (bundle.Info is { Renamed: false })
        {
            await host.Out.WriteLineAsync(Prefix + "this bundle is not renamed, and must not be posted");
        }

        var capture = BundleReplay.CaptureOf(bundle, path);
        if (!capture.Succeeded)
        {
            Report(capture.Failure);
            return 0;
        }

        var engine = BundleReplay.EngineOf(bundle);
        var planned = BundleReplay.Plan(bundle, engine, path, cancellationToken);
        if (!planned.Succeeded)
        {
            Report(planned.Failure);
            return 0;
        }

        var (query, dialect) = planned.Value;
        var describer = BundleDescribers.For(dialect, host.Describers, bundle.ExplainSettings, engine, path);
        if (!describer.Succeeded)
        {
            Report(describer.Failure);
            return 0;
        }

        // Before the replay, so that a bundle whose answer cannot be read is not replayed for nothing.
        var expected = BundleReplay.ExpectedOf(bundle, path);
        if (!expected.Succeeded)
        {
            Report(expected.Failure);
            return 0;
        }

        var replayed = await BundleReplay.RunAsync(
            query,
            dialect,
            capture.Value,
            describer.Value,
            new ReplayLines(reporter.Trace.Log, host.Out, verbose),
            host.Time,
            cancellationToken
        );
        if (replayed is ReplayOutcome.Missed missed)
        {
            Report(BundleErrors.CallMissing(path, missed.Method, BundleErrors.MadeBy(bundle.Info?.ToolVersion)));
            return 0;
        }

        return await SayAsync(host.Out, bundle.Info, expected.Value, replayed);
    }

    // The outcome's lines, and the command's exit code.
    private static async Task<int> SayAsync(
        TextWriter output,
        BundleInfo? info,
        ReplayOutcome? expected,
        ReplayOutcome replayed
    )
    {
        if (expected is null)
        {
            await output.WriteLineAsync(Prefix + "replayed: " + OneLine.Of(TextOf(replayed)));
            return 0;
        }

        var differences = DifferencesOf(expected, replayed);
        if (differences.Count == 0)
        {
            await output.WriteLineAsync(Prefix + "reproduced: the pipeline gives this answer");
            return 0;
        }

        await output.WriteLineAsync(Prefix + "not reproduced");
        foreach (var difference in differences)
        {
            await output.WriteLineAsync(Prefix + "  " + OneLine.Of(difference));
        }

        if (info is not null)
        {
            await output.WriteLineAsync(
                Prefix
                    + $"the bundle was made by sqlsource {OneLine.Of(info.ToolVersion)}, sidecar format "
                    + $"{info.FormatVersion}; this is sqlsource {OneLine.Of(Cli.Version)}, sidecar format "
                    + $"{SidecarFormat.Version}"
            );
        }

        return 2;
    }

    // What sets the expected answer apart from the replayed one: none when they are the same.  Two entries are the
    // same when a sidecar writes them the same.
    private static IReadOnlyList<string> DifferencesOf(ReplayOutcome expected, ReplayOutcome replayed) =>
        (expected, replayed) switch
        {
            (ReplayOutcome.Described left, ReplayOutcome.Described right) => EntryDifferences.Of(
                left.Entry,
                right.Entry
            ),
            _ when expected == replayed => [],
            _ => [$"expected {ShortTextOf(expected)}, replayed {ShortTextOf(replayed)}"],
        };

    // An outcome as "replayed:" prints it: an entry whole, as a sidecar of it alone writes its key and its object, on
    // one line; a refusal by its id and its step.
    private static string TextOf(ReplayOutcome outcome) =>
        outcome is ReplayOutcome.Described described ? EntryText(described.Entry) : ShortTextOf(outcome);

    private static string ShortTextOf(ReplayOutcome outcome) =>
        outcome switch
        {
            ReplayOutcome.Described => "an entry",
            ReplayOutcome.Refused { Step: { } step } refused => $"{refused.Id} at {step}",
            ReplayOutcome.Refused refused => refused.Id,
            // A miss is reported, and never compared or printed.
            _ => throw new InvalidOperationException($"An outcome of {outcome.GetType().Name} has no text."),
        };

    private static string EntryText(SidecarEntry entry)
    {
        var text = SidecarWriter.Write(
            new Sidecar(SidecarFormat.Version, Cli.Version, new EquatableArray<SidecarEntry>([entry]))
        );
        var queries =
            JsonNode.Parse(text)?["queries"]
            ?? throw new InvalidOperationException("The sidecar of an entry has no queries.");
        return queries.ToJsonString(EntryOptions);
    }

    // An error at the bundle as it is printed: at its full path, or, for a token that holds "=" or ";", at no file,
    // with the path named by its position, in its text too.
    private static ToolDiagnostic AsNamed(ToolDiagnostic error, UsageValue argument, string path)
    {
        if (argument.MayBePrintedAsPath)
        {
            return error;
        }

        var named = argument.NamedAsPath(path);
        string[] arguments =
        [
            .. error.Arguments.Select(text =>
                text.Replace(path, named, StringComparison.Ordinal)
                    .Replace(OneLine.Of(path), named, StringComparison.Ordinal)
            ),
        ];
        return ToolDiagnostic.Create(error.Descriptor, arguments).WithLines(new ContinuationLine("bundle", named));
    }

    // The values of --log, in order.
    private static UsageValue[] LogsOf(Usage usage) =>
        [.. usage.Values.Where(static value => value.Option?.Name == DescribeCommand.LogOption)];
}
