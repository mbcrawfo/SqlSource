using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Reads a project manifest: lines of <c>key=value</c>, where the key ends at the first <c>=</c> and the value is
/// the rest of the line as it is.
/// </summary>
/// <remarks>
/// The format is a contract with <c>build/SqlSource.targets</c> of the package, which may be of another version than
/// the tool.  So a key that is not known is passed over, and so is an empty line: both are how a later package adds
/// to the format.  A manifest of another version is not read at all.
/// </remarks>
internal static class ManifestReader
{
    /// <summary>The version of the format that this reader reads.</summary>
    public const string Version = "1";

    private const string VersionKey = "SqlSourceManifest";
    private const string PropertyPrefix = "Property.";
    private const string FilePrefix = "File.";

    private static readonly char[] ConstantSeparators = [';', ','];

    /// <summary>
    /// The manifest, or null and why the text is not one.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="reason">What is wrong, as the end of a sentence; empty when nothing is.</param>
    public static ProjectManifest? Read(string text, out string reason)
    {
        // WriteLinesToFile writes none, but an editor or a copy may add one.
        var rest = text.AsSpan().TrimStart('﻿');

        string? project = null;
        var targetFramework = "";
        var langVersion = "";
        var constants = "";
        var properties = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = new List<(string Path, ImmutableDictionary<string, string>.Builder Metadata)>();
        var compileFiles = ImmutableArray.CreateBuilder<string>();

        var number = 0;
        while (TryReadLine(ref rest, out var line))
        {
            number++;
            if (number == 1)
            {
                if (!IsVersionLine(line, out reason))
                {
                    return null;
                }

                continue;
            }

            if (line.IsEmpty)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                reason = string.Create(CultureInfo.InvariantCulture, $"line {number} has no '='");
                return null;
            }

            var key = line[..separator];
            var value = line[(separator + 1)..].ToString();
            if (key.StartsWith(FilePrefix, StringComparison.Ordinal))
            {
                if (files.Count == 0)
                {
                    reason = string.Create(
                        CultureInfo.InvariantCulture,
                        $"line {number} gives metadata of a file before any file"
                    );
                    return null;
                }

                Set(files[^1].Metadata, key[FilePrefix.Length..], value);
            }
            else if (key.StartsWith(PropertyPrefix, StringComparison.Ordinal))
            {
                Set(properties, key[PropertyPrefix.Length..], value);
            }
            else
            {
                switch (key)
                {
                    case "Project":
                        project = value;
                        break;
                    case "TargetFramework":
                        targetFramework = value;
                        break;
                    case "LangVersion":
                        langVersion = value;
                        break;
                    case "DefineConstants":
                        constants = value;
                        break;
                    case "File":
                        files.Add((value, ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal)));
                        break;
                    case "Compile":
                        compileFiles.Add(value);
                        break;
                    default:
                        // A key of a later package.
                        break;
                }
            }
        }

        if (number == 0)
        {
            reason = "it is empty";
            return null;
        }

        if (string.IsNullOrEmpty(project))
        {
            reason = "it names no project";
            return null;
        }

        reason = "";
        return new ProjectManifest
        {
            ProjectPath = project,
            TargetFramework = targetFramework,
            LangVersion = langVersion,
            DefineConstants =
            [
                .. constants.Split(
                    ConstantSeparators,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ],
            Properties = properties.ToImmutable(),
            Files = [.. files.ConvertAll(static file => new ManifestFile(file.Path, file.Metadata.ToImmutable()))],
            CompileFiles = compileFiles.ToImmutable(),
        };
    }

    private static bool IsVersionLine(ReadOnlySpan<char> line, out string reason)
    {
        const string Prefix = VersionKey + "=";
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            reason = $"its first line is not '{Prefix}{Version}'";
            return false;
        }

        var version = line[Prefix.Length..];
        if (!version.SequenceEqual(Version))
        {
            reason =
                $"it has version '{version}' of the format and this tool reads version {Version}.  "
                + "The SqlSource package and the sqlsource tool are out of step: update the older one";
            return false;
        }

        reason = "";
        return true;
    }

    // A value that is empty is one that is not set, and is left out.
    private static void Set(ImmutableDictionary<string, string>.Builder values, ReadOnlySpan<char> name, string value)
    {
        if (value.Length > 0)
        {
            values[name.ToString()] = value;
        }
    }

    // The next line, without its line break: "\n", or "\r\n" as MSBuild writes it on Windows.
    private static bool TryReadLine(ref ReadOnlySpan<char> rest, out ReadOnlySpan<char> line)
    {
        if (rest.IsEmpty)
        {
            line = default;
            return false;
        }

        var end = rest.IndexOf('\n');
        if (end < 0)
        {
            line = rest;
            rest = default;
        }
        else
        {
            line = rest[..end];
            rest = rest[(end + 1)..];
        }

        if (!line.IsEmpty && line[^1] == '\r')
        {
            line = line[..^1];
        }

        return true;
    }
}
