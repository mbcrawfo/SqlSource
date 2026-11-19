using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlSource.Repository.Tests.Validation;

/// <summary>
/// The patterns of a shell script that break it under Git Bash on Windows, read line by line: a <c>docker</c> command
/// that is not run through <c>run_docker</c>, and a host path that reaches a Windows program inside a file or a value
/// without <c>native_path</c>.  Both functions are in <c>tools/platform.sh</c>, which is exempt.
/// </summary>
/// <remarks>
/// A variable holds a host path when the script assigns it from <c>mktemp</c>, from <c>pwd</c>, or from a string that
/// starts with another such variable, <c>$BASH</c>, <c>$HOME</c>, <c>$TMPDIR</c> or <c>$PWD</c>.  A name that ends in
/// <c>_native</c>, and a value that calls <c>native_path</c> or <c>windows_path</c>, is converted.  Such a variable is
/// unconverted where it is used in what is written into a file a Windows program reads (by a heredoc, <c>printf</c>
/// or <c>echo</c>, the file named or held by a variable), in the value of an option (<c>-p:Name=</c>,
/// <c>--name=</c>, quoted or not), in an exported or command-prefixed assignment, in a <c>printf</c> that writes JSON,
/// or on a line that runs <c>run_docker</c>.  A path given alone as an argument is left out: Git Bash converts that
/// one itself.
/// </remarks>
internal static partial class ScriptPortability
{
    private const string Helper = "tools/platform.sh";
    private const string BareDocker = "bare docker";
    private const string Unconverted = "unconverted host path";

    // The files that dotnet, MSBuild or cmd read, which Git Bash never converts a path in.
    private static readonly string[] ReadByWindows =
    [
        ".config",
        ".csproj",
        ".props",
        ".targets",
        ".json",
        ".slnf",
        ".env",
        ".cmd",
        ".bat",
    ];
    private static readonly string[] HostRoots = ["BASH", "HOME", "TMPDIR", "PWD"];

    public static IReadOnlyList<string> Violations(string path, string text)
    {
        if (path == Helper)
        {
            return [];
        }

        var violations = new List<string>();
        var variables = new Variables();
        string? heredocEnd = null;
        var heredocChecked = false;
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            void Report(string rule) => violations.Add($"{path}:{index + 1}: {rule}: {line.Trim()}");

            if (heredocEnd is not null)
            {
                if (line.Trim() == heredocEnd)
                {
                    heredocEnd = null;
                }
                else if (heredocChecked && variables.UsesHostPath(line))
                {
                    Report(Unconverted);
                }

                continue;
            }

            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (BareDockerCommand().IsMatch(line))
            {
                Report(BareDocker);
            }

            var heredoc = HeredocStart().Match(line);
            if (InContext(heredoc.Success ? line[..heredoc.Index] : line, variables))
            {
                Report(Unconverted);
            }

            variables.Assign(line);

            if (heredoc.Success)
            {
                heredocEnd = heredoc.Groups["end"].Value;
                heredocChecked =
                    heredoc.Groups["quote"].Value.Length == 0
                    && FileRedirection()
                        .Matches(line[..heredoc.Index])
                        .Any(target => variables.IsReadByWindows(target.Groups["file"].Value));
            }
        }

        return violations;
    }

    // A line that hands a host path to a Windows program outside a heredoc's text.
    private static bool InContext(string line, Variables variables)
    {
        var converted = WithoutConversions(line);
        foreach (Match target in FileRedirection().Matches(converted))
        {
            if (
                variables.IsReadByWindows(target.Groups["file"].Value)
                && variables.UsesHostPath(converted.Remove(target.Index, target.Length))
            )
            {
                return true;
            }
        }

        if (OptionValue().Matches(converted).Any(option => variables.UsesHostPath(option.Value)))
        {
            return true;
        }

        if (ExportedValue().Match(converted) is { Success: true } exported)
        {
            return variables.UsesHostPath(exported.Groups["value"].Value);
        }

        if (PrefixedValue().Match(converted) is { Success: true } prefixed)
        {
            return variables.UsesHostPath(prefixed.Groups["value"].Value);
        }

        if (
            converted.Contains("printf", StringComparison.Ordinal)
            && converted.Contains("{\"", StringComparison.Ordinal)
        )
        {
            return variables.UsesHostPath(converted);
        }

        return RunDockerCommand().IsMatch(converted)
            && variables.UsesHostPath(Redirections().Replace(converted, string.Empty));
    }

    // The text without its calls of native_path and windows_path, whose arguments are converted.
    private static string WithoutConversions(string text)
    {
        var result = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var call = Conversion().Match(text, index);
            if (!call.Success)
            {
                _ = result.Append(text, index, text.Length - index);
                break;
            }

            _ = result.Append(text, index, call.Index - index);
            var depth = 0;
            var end = call.Index + 1;
            for (; end < text.Length; end++)
            {
                if (text[end] == '(')
                {
                    depth++;
                }
                else if (text[end] == ')' && --depth == 0)
                {
                    break;
                }
            }

            index = end + 1;
        }

        return result.ToString();
    }

    // The variables of a script as it is read: which hold a host path, and the text each was last assigned.
    private sealed class Variables
    {
        private readonly HashSet<string> _hostPaths = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, string> _values = [with(StringComparer.Ordinal)];

        public bool UsesHostPath(string text) =>
            Reference().Matches(WithoutConversions(text)).Any(reference => IsHostPath(reference.Groups["name"].Value));

        // Whether a redirection's target, a path or a variable that holds one, is a file a Windows program reads.
        public bool IsReadByWindows(string target)
        {
            var file = target.Trim('"', '\'');
            if (WholeReference().Match(file) is { Success: true } whole)
            {
                file = _values.GetValueOrDefault(whole.Groups["name"].Value, string.Empty).Trim('"', '\'');
            }

            return ReadByWindows.Any(extension => file.EndsWith(extension, StringComparison.Ordinal));
        }

        // Follows an assignment: its variable holds a host path, or no longer does.
        public void Assign(string line)
        {
            if (Assignment().Match(line) is not { Success: true } assignment)
            {
                return;
            }

            var name = assignment.Groups["name"].Value;
            var value = assignment.Groups["value"].Value;
            _values[name] = value;
            var isHostPath =
                !value.Contains("native_path", StringComparison.Ordinal)
                && !value.Contains("windows_path", StringComparison.Ordinal)
                && (
                    FromMktempOrPwd().IsMatch(value)
                    || (
                        LeadingReference().Match(value) is { Success: true } leading
                        && (
                            HostRoots.Contains(leading.Groups["name"].Value, StringComparer.Ordinal)
                            || IsHostPath(leading.Groups["name"].Value)
                        )
                    )
                );
            if (isHostPath)
            {
                _ = _hostPaths.Add(name);
            }
            else
            {
                _ = _hostPaths.Remove(name);
            }
        }

        private bool IsHostPath(string name) =>
            !name.EndsWith("_native", StringComparison.Ordinal) && _hostPaths.Contains(name);
    }

    [GeneratedRegex(@"(?:^|[;&|(]|\$\(|\b(?:then|do|if|until|while|elif|else)\s|!)\s*docker(?:\s|$)")]
    private static partial Regex BareDockerCommand();

    [GeneratedRegex(@"(?:^|[;&|(]|\$\(|\b(?:then|do|if|until|while|elif|else)\s|!)\s*run_docker\s")]
    private static partial Regex RunDockerCommand();

    [GeneratedRegex(@"\$\((?:native|windows)_path\b")]
    private static partial Regex Conversion();

    [GeneratedRegex(@"\$\{?(?<name>[A-Za-z_]\w*)")]
    private static partial Regex Reference();

    [GeneratedRegex(@"^\$\{?(?<name>[A-Za-z_]\w*)\}?$")]
    private static partial Regex WholeReference();

    [GeneratedRegex(@"^\s*""?\$\{?(?<name>[A-Za-z_]\w*)")]
    private static partial Regex LeadingReference();

    [GeneratedRegex(@"\$\(\s*(?:mktemp|pwd)\b|&&\s*pwd\b")]
    private static partial Regex FromMktempOrPwd();

    [GeneratedRegex(@"^\s*(?:local\s+|export\s+|readonly\s+)?(?<name>[A-Za-z_]\w*)=(?<value>.*)$")]
    private static partial Regex Assignment();

    // An option and its value, which is quoted only when the quote follows its '=', as in -p:Feed="$x".
    [GeneratedRegex(@"(?<=^|[\s""'])-{1,2}[A-Za-z][\w-]*[:=][^\s""']*(?:(?<==)""[^""]*"")?")]
    private static partial Regex OptionValue();

    [GeneratedRegex(@"^\s*export\s+[A-Za-z_]\w*=(?<value>.*)$")]
    private static partial Regex ExportedValue();

    // An assignment before a command, whose value holds no command substitution: VAR="$x" command.
    [GeneratedRegex(
        @"^\s*[A-Za-z_]\w*=(?<value>""(?:[^""$]|\$\{?\w+\}?)*""|'[^']*'|(?:[^\s""'$(]|\$\{?\w+\}?)+)\s+[^\s|&;]"
    )]
    private static partial Regex PrefixedValue();

    [GeneratedRegex(@"<<-?\s*(?<quote>['""]?)(?<end>\w+)\k<quote>")]
    private static partial Regex HeredocStart();

    // A redirection of the output into a file, not into another descriptor: >"$x/a.json", >>$log.
    [GeneratedRegex(@"(?<![\d&<>])>>?\s*(?<file>""[^""]*""|'[^']*'|[^\s;|&<>()]+)")]
    private static partial Regex FileRedirection();

    [GeneratedRegex(@"\d?[<>]+&?\s*(?:""[^""]*""|\S+)")]
    private static partial Regex Redirections();
}
