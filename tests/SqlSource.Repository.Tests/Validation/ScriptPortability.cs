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
/// starts with another such variable, <c>$BASH</c> or <c>$HOME</c>.  A name that ends in <c>_native</c>, and a value
/// that calls <c>native_path</c> or <c>windows_path</c>, is converted.  Such a variable is unconverted where it is used
/// in a heredoc that writes a file read by dotnet or MSBuild, in the value of an option (<c>-p:Name=</c>,
/// <c>--name=</c>), in an exported or command-prefixed assignment, in a <c>printf</c> that writes JSON, or on a line
/// that runs <c>run_docker</c>.  A path given alone as an argument is left out: Git Bash converts that one itself.
/// </remarks>
internal static partial class ScriptPortability
{
    private const string Helper = "tools/platform.sh";
    private const string BareDocker = "bare docker";
    private const string Unconverted = "unconverted host path";

    private static readonly string[] ReadByDotnet =
    [
        ".config",
        ".csproj",
        ".props",
        ".targets",
        ".json",
        ".slnf",
        ".env",
    ];
    private static readonly string[] HostRoots = ["BASH", "HOME", "TMPDIR", "PWD"];

    public static IReadOnlyList<string> Violations(string path, string text)
    {
        if (path == Helper)
        {
            return [];
        }

        var violations = new List<string>();
        var hostPaths = new HashSet<string>(StringComparer.Ordinal);
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
                else if (heredocChecked && UsesHostPath(line, hostPaths))
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

            if (InContext(line, hostPaths))
            {
                Report(Unconverted);
            }

            Assign(line, hostPaths);

            if (HeredocStart().Match(line) is { Success: true } heredoc)
            {
                heredocEnd = heredoc.Groups["end"].Value;
                var target = Redirection().Match(line[..heredoc.Index]);
                heredocChecked =
                    heredoc.Groups["quote"].Value.Length == 0
                    && target.Success
                    && ReadByDotnet.Any(extension =>
                        target.Groups["file"].Value.Trim('"').EndsWith(extension, StringComparison.Ordinal)
                    );
            }
        }

        return violations;
    }

    // A line that hands a host path to a Windows program outside a heredoc.
    private static bool InContext(string line, HashSet<string> hostPaths)
    {
        var converted = WithoutConversions(line);
        if (OptionValue().Matches(converted).Any(option => UsesHostPath(option.Value, hostPaths)))
        {
            return true;
        }

        if (ExportedValue().Match(converted) is { Success: true } exported)
        {
            return UsesHostPath(exported.Groups["value"].Value, hostPaths);
        }

        if (PrefixedValue().Match(converted) is { Success: true } prefixed)
        {
            return UsesHostPath(prefixed.Groups["value"].Value, hostPaths);
        }

        if (
            converted.Contains("printf", StringComparison.Ordinal)
            && converted.Contains("{\"", StringComparison.Ordinal)
        )
        {
            return UsesHostPath(converted, hostPaths);
        }

        return RunDockerCommand().IsMatch(converted)
            && UsesHostPath(Redirections().Replace(converted, string.Empty), hostPaths);
    }

    private static bool UsesHostPath(string text, HashSet<string> hostPaths) =>
        Reference()
            .Matches(WithoutConversions(text))
            .Any(reference => IsHostPath(reference.Groups["name"].Value, hostPaths));

    private static bool IsHostPath(string name, HashSet<string> hostPaths) =>
        !name.EndsWith("_native", StringComparison.Ordinal) && hostPaths.Contains(name);

    // Follows an assignment: its variable holds a host path, or no longer does.
    private static void Assign(string line, HashSet<string> hostPaths)
    {
        if (Assignment().Match(line) is not { Success: true } assignment)
        {
            return;
        }

        var name = assignment.Groups["name"].Value;
        var value = assignment.Groups["value"].Value;
        var isHostPath =
            !value.Contains("native_path", StringComparison.Ordinal)
            && !value.Contains("windows_path", StringComparison.Ordinal)
            && (
                FromMktempOrPwd().IsMatch(value)
                || (
                    LeadingReference().Match(value) is { Success: true } leading
                    && (
                        HostRoots.Contains(leading.Groups["name"].Value, StringComparer.Ordinal)
                        || IsHostPath(leading.Groups["name"].Value, hostPaths)
                    )
                )
            );
        if (isHostPath)
        {
            _ = hostPaths.Add(name);
        }
        else
        {
            _ = hostPaths.Remove(name);
        }
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

    [GeneratedRegex(@"(?:^|[;&|(]|\$\(|\b(?:then|do|if|until|while|elif|else)\s|!)\s*docker(?:\s|$)")]
    private static partial Regex BareDockerCommand();

    [GeneratedRegex(@"(?:^|[;&|(]|\$\(|\b(?:then|do|if|until|while|elif|else)\s|!)\s*run_docker\s")]
    private static partial Regex RunDockerCommand();

    [GeneratedRegex(@"\$\((?:native|windows)_path\b")]
    private static partial Regex Conversion();

    [GeneratedRegex(@"\$\{?(?<name>[A-Za-z_]\w*)")]
    private static partial Regex Reference();

    [GeneratedRegex(@"^\s*""?\$\{?(?<name>[A-Za-z_]\w*)")]
    private static partial Regex LeadingReference();

    [GeneratedRegex(@"\$\(\s*(?:mktemp|pwd)\b|&&\s*pwd\b")]
    private static partial Regex FromMktempOrPwd();

    [GeneratedRegex(@"^\s*(?:local\s+|export\s+|readonly\s+)?(?<name>[A-Za-z_]\w*)=(?<value>.*)$")]
    private static partial Regex Assignment();

    [GeneratedRegex(@"(?<=^|[\s""'])-{1,2}[A-Za-z][\w-]*[:=][^\s""']*")]
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

    [GeneratedRegex(@">\s*(?<file>""[^""]*""|\S+)\s*$")]
    private static partial Regex Redirection();

    [GeneratedRegex(@"\d?[<>]+&?\s*(?:""[^""]*""|\S+)")]
    private static partial Regex Redirections();
}
