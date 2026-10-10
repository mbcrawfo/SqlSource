using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Projects;
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
    /// Runs the command with a host that is made here, and listens for Ctrl+C.
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
            // RunAsync's catch reports to the host, and there is none.
            new Reporter(error).Report(Failure(exception, Environment.GetEnvironmentVariable));
            return 1;
        }

        using var interrupt = new CancellationTokenSource();
        void Cancel(object? sender, ConsoleCancelEventArgs e)
        {
            // The run ends by its own road, with its exit code, and not by the process being killed.
            e.Cancel = true;
            interrupt.Cancel();
        }

        Console.CancelKeyPress += Cancel;
        try
        {
            return RunAsync(args, host, interrupt.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= Cancel;
        }
    }

    /// <summary>
    /// Runs the command with a host that the caller gives.  The tests call this.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        var reporter = new Reporter(host.Error);
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            var root = BuildCommands(host, reporter);

            var usage = UsageCheck.Check(root, args);
            if (usage.Messages.Count > 0)
            {
                // These lines have no id and do not go through the reporter.
                foreach (var line in usage.Messages)
                {
                    await host.Error.WriteLineAsync(line);
                }

                return 1;
            }

            var parsed = root.Parse(args, Parser);
            if (parsed.Errors.Count > 0 || !usage.IsReadTheSameBy(parsed))
            {
                // UsageCheck lets nothing through that System.CommandLine rejects or reads another way, as far as
                // the tests know.  If something is, nothing runs, and System.CommandLine's message is not written:
                // it may repeat a token.
                await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
                return 1;
            }

            var invocation = new InvocationConfiguration
            {
                Output = host.Out,
                Error = host.Error,
                // An exception comes here, to the catch below.
                EnableDefaultExceptionHandler = false,
                // Cli.Run listens for Ctrl+C itself.
                ProcessTerminationTimeout = null,
            };
            var exitCode = await parsed.InvokeAsync(invocation, cancellationToken);
            return reporter.Count > 0 ? 1 : exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception exception)
        {
            reporter.Report(Failure(exception, host.GetEnvironmentVariable));
            return 1;
        }
    }

    internal static Command BuildCommands(ToolHost host, Reporter reporter)
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
            Describe(host, reporter),
        };

        // System.CommandLine's own answer to no command is an error.
        root.SetAction(static parsed => new HelpAction().Invoke(parsed));
        return root;
    }

    private static Command Describe(ToolHost host, Reporter reporter)
    {
        var path = new Argument<string?>("path")
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one.  The current directory "
                + "when left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        // One value each time it is given, and it may be given several times: UsageCheck reads the token after it as
        // its value and no further.
        var project = new Option<string[]>("--project")
        {
            Description = "A project to run on, of the solution.  May be given several times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var describe = new Command("describe", "Finds the projects to describe and reads what the compiler is given")
        {
            path,
            project,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                if (RunUnitFinder.Find(parsed.GetValue(path), host.WorkingDirectory, reporter) is not { } unit)
                {
                    return 0;
                }

                var manifests = await RunProjects.FindAsync(
                    unit,
                    parsed.GetValue(project) ?? [],
                    host,
                    reporter,
                    cancellationToken
                );
                if (manifests.IsEmpty && reporter.Count == 0)
                {
                    // A run that found nothing to do must not look like one that did it.
                    await host.Out.WriteLineAsync($"sqlsource: no project of '{OneLine.Of(unit.Path)}' uses SqlSource");
                }

                return 0;
            }
        );
        return describe;
    }

    private static ToolDiagnostic Failure(Exception exception, Func<string, string?> getEnvironmentVariable)
    {
        var failure = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message
        );
        if (string.IsNullOrEmpty(getEnvironmentVariable(DebugVariable)))
        {
            return failure;
        }

        var trace = exception
            .ToString()
            .Split('\n')
            .Select(static line => new ContinuationLine("trace", line.TrimEnd('\r')));
        return failure.WithLines([.. trace]);
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
}
