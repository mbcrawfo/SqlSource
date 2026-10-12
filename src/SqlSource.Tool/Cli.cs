using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>sqlsource</c> command.
/// </summary>
public static class Cli
{
    /// <summary>
    /// The name of the environment variable that adds the stack trace to <c>SQLSRC200</c>.
    /// </summary>
    internal const string DebugVariable = "SQLSOURCE_DEBUG";

    /// <summary>
    /// What ends a <c>describe</c> that reported an error and wrote no log: there is no hint under each error, and
    /// this one line says how to get a record of the run.
    /// </summary>
    internal const string RunAgainLine = "sqlsource: run again with --log <file> for a record of what the run did";

    /// <summary>
    /// How System.CommandLine reads a command line for the tool.
    /// </summary>
    internal static readonly ParserConfiguration Parser = new()
    {
        // A token is read as it stands: "@name" is no file to read, and "-ab" is not "-a -b".
        ResponseFileTokenReplacer = null,
        EnablePosixBundling = false,
    };

    /// <summary>
    /// The informational version of the tool's assembly: the package's version and the commit.
    /// </summary>
    internal static string Version { get; } =
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? PackageVersion.Prefix;

    /// <summary>
    /// Runs the command with the console, the current directory and the environment of the process.
    /// </summary>
    /// <param name="args">The command line, without the name of the program.</param>
    /// <returns>
    /// <c>0</c> on success, <c>1</c> when an error was reported, a wrong command line included, and <c>2</c> when
    /// <c>--check</c> found a difference and nothing failed.
    /// </returns>
    public static int Run(string[] args) => Run(args, ToolHost.Create, Console.Error);

    /// <summary>
    /// Runs the command with a host that is made here, and listens for Ctrl+C, <c>SIGTERM</c> and <c>SIGHUP</c>.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="createHost">Makes the host.  The real one asks for the current directory, which can throw.</param>
    /// <param name="error">Where the error goes when there is no host to report to.</param>
    internal static int Run(string[] args, Func<ToolHost> createHost, TextWriter error)
    {
        ToolHost host;
        try
        {
            host = createHost();
        }
        catch (Exception exception)
        {
            // RunCommandAsync's catch reports to the host, and there is none.
            new Reporter(error).Report(UnexpectedFailure.Of(exception, Environment.GetEnvironmentVariable));
            return 1;
        }

        using var interrupt = new CancellationTokenSource();
        using var ended = new ManualResetEventSlim();
        var signals = 0;
        void Cancel(object? sender, ConsoleCancelEventArgs e) => e.Cancel = Interrupt(interrupt, ref signals);
        void End(PosixSignalContext context) => context.Cancel = Terminate(interrupt, ref signals, ended.Wait);

        Console.CancelKeyPress += Cancel;

        // SIGTERM is what "timeout", a cancelled CI job and an editor send, and SIGHUP what a closed terminal and a
        // lost session do.  Left alone, either ends the process at once, with the programs the run started still
        // running and its temporary folder left.  On Windows they are the shutdown and the closed console.
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, End);
        using var hangUp = PosixSignalRegistration.Create(PosixSignal.SIGHUP, End);
        try
        {
            return RunAsync(args, host, interrupt.Token).GetAwaiter().GetResult();
        }
        finally
        {
            ended.Set();
            Console.CancelKeyPress -= Cancel;
        }
    }

    /// <summary>
    /// What Ctrl+C does.  The first time, the run is cancelled and ends by its own road, with its exit code, and
    /// the process is kept.  A second time, while the run has not ended, the process is left to be killed: a run
    /// that waits on a database may not look at its token.
    /// </summary>
    /// <param name="interrupt">Cancels the run.</param>
    /// <param name="signals">
    /// How many times the run was told to end, by any of the three.  Two that come at once, each on a thread of
    /// its own, are counted apart, so only one of them is the first.
    /// </param>
    /// <returns>Whether the process is kept.</returns>
    internal static bool Interrupt(CancellationTokenSource interrupt, ref int signals)
    {
        if (Interlocked.Increment(ref signals) > 1)
        {
            return false;
        }

        interrupt.Cancel();
        return true;
    }

    /// <summary>
    /// What <c>SIGTERM</c> and <c>SIGHUP</c> do: what Ctrl+C does, and the first time it returns only when the run
    /// has ended.  Windows ends the process when the handler of a shutdown or of a closed console returns,
    /// whatever it answers, so a handler that returned at once would leave the run no time to end.
    /// </summary>
    /// <param name="interrupt">Cancels the run.</param>
    /// <param name="signals">How many times the run was told to end, by any of the three.</param>
    /// <param name="waitForTheEnd">Returns when the run has ended.</param>
    /// <returns>Whether the process is kept.</returns>
    internal static bool Terminate(CancellationTokenSource interrupt, ref int signals, Action waitForTheEnd)
    {
        if (!Interrupt(interrupt, ref signals))
        {
            return false;
        }

        waitForTheEnd();
        return true;
    }

    /// <summary>
    /// Runs the command with a host that the caller gives.  The tests call this.
    /// </summary>
    /// <remarks>
    /// The log of the run is opened and closed here, though <c>--log</c> is an option of <c>describe</c>: the error
    /// of an exception is reported here and the exit code is decided here, and both belong in the log.
    /// </remarks>
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        var state = new RunState(host);
        try
        {
            // The command first, and the reporter only after it: the command replaces the reporter.
            var commandExitCode = await RunCommandAsync(args, state, cancellationToken);
            var exitCode = ExitCodeOf(state.Reporter, commandExitCode);
            if (state.MayPoint && !state.Cancelled && state.Reporter.Count > state.Reporter.DifferenceCount)
            {
                await host.Error.WriteLineAsync(RunAgainLine);
            }

            return state.Log is null ? exitCode : End(state.Log, exitCode, state);
        }
        finally
        {
            // A path that leaves the method by an exception of its own still closes the file.
            state.Log?.Dispose();
        }
    }

    // The steps of a run, to the end of its command, and the three things that end one early.  It gives what the
    // command gave, 1 for a run that did not start or was cancelled, and 0 for one an exception ended: the exit
    // code of that one comes from the reporter.
    private static async Task<int> RunCommandAsync(string[] args, RunState state, CancellationToken cancellationToken)
    {
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            if (
                await ReadCommandLineAsync(args, state.Host) is not { } commandLine
                || !OpenLog(commandLine, args, state)
            )
            {
                return 1;
            }

            return await InvokeAsync(commandLine, state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Cancelled = true;
            return 1;
        }
        catch (DescriberFaultException fault)
        {
            // The error of an exception that a describer threw, already without the connection's value.
            state.Reporter.Report(fault.Diagnostic);
            return 0;
        }
        catch (Exception exception)
        {
            state.Reporter.Report(UnexpectedFailure.Of(exception, state.Host.GetEnvironmentVariable));
            return 0;
        }
    }

    // Reads the command line with both readers.  Null when it is wrong, with its lines written: they have no id
    // and do not go through the reporter.
    private static async Task<ParsedCommandLine?> ReadCommandLineAsync(string[] args, ToolHost host)
    {
        var root = BuildCommands();

        var usage = UsageCheck.Check(root, args);
        var messages = usage.Messages.Count > 0 ? usage.Messages : DescribeCommand.CheckUsage(usage);
        if (messages.Count > 0)
        {
            foreach (var line in messages)
            {
                await host.Error.WriteLineAsync(line);
            }

            return null;
        }

        var parsed = root.Parse(args, Parser);
        if (
            parsed.Errors.Count > 0
            || !usage.IsReadTheSameBy(parsed)
            || !DescribeCommand.ReadsConnectionsAs(usage, parsed)
        )
        {
            // UsageCheck lets nothing through that System.CommandLine rejects or reads another way, as far as
            // the tests know.  If something is, nothing runs, and System.CommandLine's message is not written:
            // it may repeat a token.  The values of --connection are held too: the log's arguments is
            // redacted from one reading and the describer is handed the other.
            await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
            return null;
        }

        // Whether the command line runs "describe", once.
        return new ParsedCommandLine(usage, parsed, DescribeCommand.Runs(parsed));
    }

    // Opens the log the run was given, and writes run.start: after the command line was read, so that a wrong one
    // is never logged, and before anything runs.  False when the log cannot be opened, which is reported: the run
    // does not start.
    private static bool OpenLog(ParsedCommandLine commandLine, string[] args, RunState state)
    {
        var host = state.Host;
        var logPath = DescribeCommand.LogPathOf(commandLine.Parsed, host.GetEnvironmentVariable);
        state.MayPoint = commandLine.RunsDescribe && logPath is null;
        if (logPath is null)
        {
            return true;
        }

        var opened = RunLogFile.Open(logPath, host.WorkingDirectory, host.Time);
        if (!opened.Succeeded)
        {
            state.Reporter.Report(opened.Failure);
            return false;
        }

        state.Log = opened.Value;
        state.Log.Write(
            "run.start",
            new("toolVersion", PackageVersion.Prefix),
            new("formatVersion", SidecarFormat.Version),
            new("runtime", RuntimeInformation.FrameworkDescription),
            new("os", RuntimeInformation.OSDescription),
            new("arguments", LoggedArguments.Of(commandLine.Usage, args))
        );
        return true;
    }

    // Gives "describe" its action with the reporter of the run, and invokes the command.
    private static async Task<int> InvokeAsync(
        ParsedCommandLine commandLine,
        RunState state,
        CancellationToken cancellationToken
    )
    {
        var host = state.Host;
        var parsed = commandLine.Parsed;
        if (commandLine.RunsDescribe)
        {
            // What the run says of itself: to the log, when it has one, and to the person watching under
            // --verbose.
            IRunLog events = state.Log is { } log ? log : NullRunLog.Instance;

            // The reporter of the run: from here on every error is also an event of the log.  The command
            // gets its action only now, so that it cannot run with another.
            state.Reporter = new Reporter(
                host.Error,
                new RunTrace(events, DescribeCommand.IsVerbose(parsed) ? host.Out : null)
            );
            DescribeCommand.Bind(parsed, host, state.Reporter, counts => state.Databases = counts);
        }

        var invocation = new InvocationConfiguration
        {
            Output = host.Out,
            Error = host.Error,
            // An exception comes to the catch of RunCommandAsync.
            EnableDefaultExceptionHandler = false,
            // Cli.Run listens for Ctrl+C, SIGTERM and SIGHUP itself.
            ProcessTerminationTimeout = null,
        };
        return await parsed.InvokeAsync(invocation, cancellationToken);
    }

    // The last event, with the exit code of the process, and the file is closed.  A log that stopped being
    // writable while the run went on did not end the run: it is an error now, once, and the exit code is 1.
    private static int End(RunLogFile log, int exitCode, RunState state)
    {
        log.Write(
            "run.end",
            new("exitCode", exitCode),
            new("durationMs", (long)state.Host.Time.GetElapsedTime(state.Started).TotalMilliseconds),
            new("databases", state.Databases)
        );
        log.Dispose();
        if (log.Failure is not { } failure)
        {
            return exitCode;
        }

        state.Reporter.Report(
            ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, log.Path, "written", failure.Message)
        );
        return 1;
    }

    /// <summary>
    /// The exit code of a run: <c>1</c> when an error other than <c>SQLSRC219</c> was reported, else what the
    /// command gave when that is not <c>0</c>, else <c>2</c> when <c>SQLSRC219</c> was reported.
    /// </summary>
    internal static int ExitCodeOf(Reporter reporter, int exitCode)
    {
        if (reporter.Count > reporter.DifferenceCount)
        {
            return 1;
        }

        if (exitCode != 0)
        {
            return exitCode;
        }

        return reporter.DifferenceCount > 0 ? 2 : 0;
    }

    internal static Command BuildCommands()
    {
        var version = new Option<bool>("--version")
        {
            Description = "Show the version of sqlsource",
            Arity = ArgumentArity.Zero,
            Action = new VersionAction(),
        };

        // Not a RootCommand: that one takes its name from the process, has options of its own under names the tool
        // does not choose, and reads "[suggest]" as a directive.
        var root = new Command("sqlsource", "Describes the SQL queries of a project for the SqlSource generator")
        {
            new HelpOption("--help", "-h", "-?"),
            version,
            DescribeCommand.Create(),
        };

        // System.CommandLine's own answer to no command is an error.
        root.SetAction(static parsed => new HelpAction().Invoke(parsed));
        return root;
    }

    // System.CommandLine's own action for a version reads the assembly the process started with, which under a test
    // is the test host.  An option's action runs in place of the command's.
    private sealed class VersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(Version);
            return 0;
        }
    }

    // A command line that both readers accept, as each read it.
    private sealed record ParsedCommandLine(Usage Usage, ParseResult Parsed, bool RunsDescribe);

    // What the steps of one run share.
    private sealed class RunState(ToolHost host)
    {
        public ToolHost Host => host;

        public long Started { get; } = host.Time.GetTimestamp();

        // Until the run has its trace an error has the console alone: a log that cannot be opened, or an
        // exception.  Neither is followed by an error of the run, so no count is split between two reporters.
        // InvokeAsync replaces it, so a step reads it here when it reports, and keeps it in no local.
        public Reporter Reporter { get; set; } = new(host.Error);

        // The log of the run, once it is open.  RunAsync closes it.
        public RunLogFile? Log { get; set; }

        // The counts of each selected database, as run.end holds them.
        public LogValue Databases { get; set; } = LogValue.ListOf([]);

        // Whether the line that ends a failed run has a reader: "describe" runs, and no log was asked for.
        public bool MayPoint { get; set; }

        public bool Cancelled { get; set; }
    }
}
