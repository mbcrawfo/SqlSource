using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Testing.Generator;

// The levels that give a setting of the generator, from the least specific: the project's property, the metadata of
// the file's item, the type's attribute, a marker of the file header and a marker of a query.
//
// It is public, though the library's types are internal, since a public test method takes it in its rows, and C#
// refuses a public method whose parameter has a type less accessible than the method.
public enum SettingLevel
{
    Property,
    Metadata,
    Attribute,
    Header,
    Query,
}

// A setting by its name at each level: the MSBuild property, which is also the metadata's name, the argument of the
// attribute, null for a setting the attribute has not, and the marker.
internal sealed record Setting(string MSBuildName, string? AttributeArgument, string Marker)
{
    public static readonly Setting InputModelSuffix = new(
        "SqlSourceInputModelSuffix",
        "InputModelSuffix",
        "input-model-suffix"
    );

    public static readonly Setting OutputModelSuffix = new(
        "SqlSourceOutputModelSuffix",
        "OutputModelSuffix",
        "output-model-suffix"
    );

    public static readonly Setting ModelNamespace = new("SqlSourceModelNamespace", "ModelNamespace", "model-namespace");

    public static readonly Setting GeneratorParameters = new("SqlSourceGeneratorParameters", "Parameters", "generator");

    public static readonly Setting TypeOverrides = new("SqlSourceTypeOverrides", null, "type-override");
}

// Settings, each at its level, and what a run is given for them: the project's properties, the type's source and the
// .sql file.  A level without a setting leaves its part as it was.
internal sealed class LevelSettings
{
    private readonly List<(SettingLevel Level, Setting Setting, string Value)> _settings = [];

    // The project's properties, or null when no setting is at the property's level.
    public IReadOnlyDictionary<string, string?>? Properties =>
        AtLevel(SettingLevel.Property) is { Length: > 0 } properties
            ? properties.ToDictionary(setting => setting.Setting.MSBuildName, setting => (string?)setting.Value)
            : null;

    // The setting at the level, with the value as that level writes it.
    public LevelSettings At(SettingLevel level, Setting setting, string value)
    {
        _settings.Add((level, setting, value));
        return this;
    }

    // The value of the setting at the property's level, or null when it has none there.
    public string? PropertyOf(Setting setting) =>
        AtLevel(SettingLevel.Property)
            .Where(property => property.Setting == setting)
            .Select(property => property.Value)
            .LastOrDefault();

    // The source with the attribute's arguments, Name = "value" joined by ", ", in place of [SqlSourceGenerate], or
    // the source unchanged when no setting is at the attribute's level.
    public string Source(string source)
    {
        var arguments = AtLevel(SettingLevel.Attribute);
        if (arguments.Length == 0)
        {
            return source;
        }

        var written = arguments.Select(argument =>
            (
                argument.Setting.AttributeArgument
                ?? throw new InvalidOperationException(
                    $"The attribute has no argument for {argument.Setting.MSBuildName}."
                )
            )
            + " = \""
            + argument.Value
            + "\""
        );
        return source.Replace(
            "[SqlSourceGenerate]",
            "[SqlSourceGenerate(" + string.Join(", ", written) + ")]",
            StringComparison.Ordinal
        );
    }

    // The file with the settings at the metadata's level merged into its metadata, the file header's markers, each
    // "-- <marker>: <value>" on its line and then a blank line, before its text, and the query's markers on the line
    // after the "-- name: <query>" line of each query named, or of every query when none is named.
    public SqlFile File(SqlFile file, params string[] queries)
    {
        var metadata = AtLevel(SettingLevel.Metadata);
        if (metadata.Length > 0)
        {
            var merged = new Dictionary<string, string>();
            foreach (var pair in file.Metadata ?? new Dictionary<string, string>())
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var setting in metadata)
            {
                merged[setting.Setting.MSBuildName] = setting.Value;
            }

            file = file with { Metadata = merged };
        }

        var header = AtLevel(SettingLevel.Header);
        var markers = AtLevel(SettingLevel.Query);
        if (header.Length == 0 && markers.Length == 0)
        {
            return file;
        }

        var text = file.Text ?? throw new InvalidOperationException($"The file {file.Path} has no text.");
        if (markers.Length > 0)
        {
            var lines = new List<string>();
            foreach (var line in text.Split('\n'))
            {
                lines.Add(line);
                if (IsNamed(line, queries))
                {
                    lines.AddRange(markers.Select(Marker));
                }
            }

            text = string.Join("\n", lines);
        }

        if (header.Length > 0)
        {
            text = string.Concat(header.Select(setting => Marker(setting) + "\n")) + "\n" + text;
        }

        return file with
        {
            Text = text,
        };
    }

    private static string Marker((SettingLevel Level, Setting Setting, string Value) setting) =>
        "-- " + setting.Setting.Marker + ": " + setting.Value;

    private static bool IsNamed(string line, string[] queries)
    {
        const string Name = "-- name: ";
        return line.StartsWith(Name, StringComparison.Ordinal)
            && (queries.Length == 0 || queries.Contains(line[Name.Length..], StringComparer.Ordinal));
    }

    private (SettingLevel Level, Setting Setting, string Value)[] AtLevel(SettingLevel level) =>
        [.. _settings.Where(setting => setting.Level == level)];
}
