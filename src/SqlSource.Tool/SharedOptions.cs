using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool;

/// <summary>
/// The options that more than one command takes, with one name, one text and one rule each, and what each command
/// reads of a command line by the same rules.
/// </summary>
/// <remarks>
/// Every option of the tool is declared so that <see cref="UsageCheck" /> reads it right.  A flag has
/// <c>Arity = ArgumentArity.Zero</c>: System.CommandLine's default for a bool takes an optional value, and the token
/// after the flag would be read as that value.  An option that takes a value takes exactly one each time it is given:
/// <c>ExactlyOne</c> when it is given once, and an array with <c>OneOrMore</c> when it may be repeated, since
/// System.CommandLine rejects a second <c>ExactlyOne</c>.  <see cref="UsageCheck" /> rejects that second one itself,
/// since System.CommandLine's message repeats the value.  An option is added to <c>--help</c> in the change that gives
/// it an effect, not before: one that is read and checked earlier is <c>Hidden</c> until then.
/// </remarks>
internal static class SharedOptions
{
    public const string Project = "--project";

    public const string Connection = "--connection";

    public const string Verbose = "--verbose";

    public const string Log = "--log";

    public const string EnvFile = "--env-file";

    /// <summary>The environment variable that gives the log's path when <c>--log</c> does not.</summary>
    public const string LogVariable = "SQLSOURCE_LOG";

    /// <summary>
    /// <c>--project</c>, one value each time it is given, and it may be given several times: <see cref="UsageCheck" />
    /// reads the token after it as its value and no further.
    /// </summary>
    public static Option<string[]> ProjectOption() =>
        new(Project)
        {
            Description = "A project to run on, of the solution.  May be given several times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

    /// <summary>
    /// <c>--connection</c>, always a name, <c>=</c> and a value, which <see cref="ProblemOfConnection" /> holds each
    /// to, and a message never repeats one.
    /// </summary>
    public static Option<string[]> ConnectionOption() =>
        new(Connection)
        {
            Description =
                "The connection string of a database, as <name>=<connection string>.  May be given several times.",
            HelpName = "name=value",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

    /// <summary><c>--verbose</c>, with the text of the command that takes it: each says something else.</summary>
    public static Option<bool> VerboseOption(string description) =>
        new(Verbose) { Description = description, Arity = ArgumentArity.Zero };

    /// <summary><c>--log</c>, whose path <see cref="LogPathOf" /> reads.</summary>
    public static Option<string> LogOption() =>
        new(Log)
        {
            Description =
                "Write what the run did to this file, one JSON object on a line.  The file is overwritten.  "
                + $"{LogVariable} gives the path when the option does not.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

    /// <summary><c>--env-file</c>, which <c>DescribeCommand.EnvFileOf</c> reads.</summary>
    public static Option<string> EnvFileOption() =>
        new(EnvFile)
        {
            Description =
                "Read the connections from this .env file instead of the one in the folder of the solution or "
                + "project; 'none' reads none.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };

    /// <summary>
    /// Whether the command line runs the command: the command is named, and it is its own action that runs and not
    /// the help's.  Until the command's <c>Bind</c> gives it its action it has none, and neither has a command line
    /// that runs it, so the answer is the same before and after.
    /// </summary>
    /// <param name="parsed">The command line.</param>
    /// <param name="command">The command's name.</param>
    public static bool Runs(ParseResult parsed, string command) =>
        parsed.CommandResult.Command.Name == command
        && ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action);

    /// <summary>Whether the command runs and was given <c>--verbose</c>.</summary>
    /// <param name="parsed">The command line.</param>
    /// <param name="command">The command's name.</param>
    public static bool IsVerbose(ParseResult parsed, string command) =>
        Runs(parsed, command) && parsed.GetValue<bool>(Verbose);

    /// <summary>
    /// The path of the run's log as it was given, by <c>--log</c> or else by <c>SQLSOURCE_LOG</c>, or null when the
    /// run has none.  A command line that does not run the command has none: the help must not empty the log of the
    /// last run.  A variable that is empty or white space is not set.
    /// </summary>
    /// <param name="parsed">The command line.</param>
    /// <param name="command">The command's name.</param>
    /// <param name="readsVariable">
    /// Whether the run reads <c>SQLSOURCE_LOG</c> when <c>--log</c> is not given: a <c>describe</c> under
    /// <c>--online</c> does not.
    /// </param>
    /// <param name="getEnvironmentVariable">The host's environment.</param>
    public static string? LogPathOf(
        ParseResult parsed,
        string command,
        bool readsVariable,
        Func<string, string?> getEnvironmentVariable
    )
    {
        if (!Runs(parsed, command))
        {
            return null;
        }

        if (parsed.GetValue<string>(Log) is { } option)
        {
            return option;
        }

        if (!readsVariable)
        {
            return null;
        }

        var variable = getEnvironmentVariable(LogVariable);
        return string.IsNullOrWhiteSpace(variable) ? null : variable;
    }

    /// <summary>The values of an option, in order.</summary>
    public static ImmutableArray<UsageValue> ValuesOf(Usage usage, string option) =>
        [.. usage.Values.Where(value => value.Option?.Name == option)];

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave an option that may be repeated the same values,
    /// in the same order.
    /// </summary>
    public static bool ReadsAs(Usage usage, ParseResult parsed, string option) =>
        ValuesOf(usage, option)
            .Select(static value => value.Text)
            .SequenceEqual(parsed.GetValue<string[]>(option) ?? [], StringComparer.Ordinal);

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave an option that takes one value, given once, the
    /// same value.
    /// </summary>
    public static bool ReadsAsOne(Usage usage, ParseResult parsed, string option) =>
        ValuesOf(usage, option)
            .Select(static value => value.Text)
            .SequenceEqual(parsed.GetValue<string>(option) is { } text ? [text] : [], StringComparer.Ordinal);

    /// <summary>
    /// What is wrong with an argument on a line with a <c>--connection</c>, as a whole line, or null: one that holds
    /// <c>=</c> or <c>;</c> is taken for the rest of a value that the shell split.
    /// </summary>
    public static string? SplitConnectionOf(UsageValue argument) =>
        argument.Text.AsSpan().IndexOfAny('=', ';') >= 0
            ? $"{Cli.LinePrefix}the argument at position {argument.Position} looks like a part of a connection string; "
                + $"put the value of '{Connection}' in quotes"
            : null;

    /// <summary>
    /// What is wrong with a value of <c>--connection</c>, as a whole line, or null: it is not a name, <c>=</c> and a
    /// value, or it names a database that an earlier one names.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="named">The names of the earlier values, which gains this one's.</param>
    public static string? ProblemOfConnection(UsageValue value, HashSet<string> named)
    {
        if (!ConnectionArgument.TryParse(value.Text, out var connection))
        {
            return $"{Cli.LinePrefix}the value of option '{Connection}' at position {value.Position} is not "
                + "<name>=<connection string>";
        }

        return named.Add(connection.Name)
            ? null
            : $"{Cli.LinePrefix}option '{Connection}' at position {value.Position} names a database that an "
                + "earlier "
                + "one names";
    }

    /// <summary>
    /// What is wrong with a value of <c>--log</c> as a path, as a whole line, or null: it is no path, or, without
    /// <c>--online</c>, it holds <c>=</c> or <c>;</c>.
    /// </summary>
    /// <param name="log">The value of <c>--log</c>.</param>
    /// <param name="isOnline">Whether the run is the build's, under <c>--online</c>, whose paths are its own.</param>
    public static string? ProblemOfLogPath(UsageValue log, bool isOnline) =>
        ProblemOfPath(log, Log, "a log's path", isOnline);

    /// <summary>
    /// What is wrong with a value of <c>--env-file</c> as a path, as a whole line, or null, by the rule of
    /// <c>--log</c>.
    /// </summary>
    /// <param name="envFile">The value of <c>--env-file</c>.</param>
    /// <param name="isOnline">Whether the run is the build's, under <c>--online</c>, whose paths are its own.</param>
    public static string? ProblemOfEnvFile(UsageValue envFile, bool isOnline) =>
        ProblemOfPath(envFile, EnvFile, "a .env file's path", isOnline);

    /// <summary>
    /// What is wrong with the value of an option that is a path, as a whole line, or null: it is no path, or, without
    /// <c>--online</c>, it holds <c>=</c> or <c>;</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="option">The option's name.</param>
    /// <param name="ofWhat">What the path is of, as the line names it: <c>a log's path</c>.</param>
    /// <param name="isOnline">Whether the run is the build's, under <c>--online</c>, whose paths are its own.</param>
    public static string? ProblemOfPath(UsageValue value, string option, string ofWhat, bool isOnline)
    {
        // What --log "$UNSET" gives.
        if (string.IsNullOrWhiteSpace(value.Text))
        {
            return $"{Cli.LinePrefix}the value of option '{option}' at position {value.Position} is not a path";
        }

        // "=" and ";" are what a connection string is made of: the value may be one typed after the option.  The
        // build's paths may hold them, as a project's name may, and the owner ruled that a path the build writes is
        // no secret, so under --online a message prints such a path in full.
        return !isOnline && !value.MayBePrintedAsPath
            ? $"{Cli.LinePrefix}the value of option '{option}' at position {value.Position} holds '=' or ';', which "
                + $"{ofWhat} may not"
            : null;
    }
}
