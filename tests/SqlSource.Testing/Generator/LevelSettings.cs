using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SqlSource.Testing.Generator;

// The levels that give a setting of the generator, from the least specific: the project's settings file, the settings
// file of the .sql file's folder, the type's attribute, a marker of the file header and a marker of a query.
//
// It is public, though the library's types are internal, since a public test method takes it in its rows, and C#
// refuses a public method whose parameter has a type less accessible than the method.
public enum SettingLevel
{
    ProjectFile,
    FolderFile,
    Attribute,
    Header,
    Query,
}

// A setting by its name at each level: the key of a settings file, the argument of the attribute, null for a setting
// the attribute has not, and the marker.  The attribute takes the value of a setting with an enum as that enum's
// member, so its value is the member's name, and as a string otherwise.
internal sealed record Setting(string Key, string? AttributeArgument, string Marker, string? AttributeEnum = null)
{
    public static readonly Setting Output = new("output", "Output", "output", "GeneratorOutput");

    public static readonly Setting CollectionType = new(
        "collectionType",
        "CollectionType",
        "collection-type",
        "GeneratorCollectionType"
    );

    public static readonly Setting InputModelSuffix = new("inputModelSuffix", "InputModelSuffix", "input-model-suffix");

    public static readonly Setting OutputModelSuffix = new(
        "outputModelSuffix",
        "OutputModelSuffix",
        "output-model-suffix"
    );

    public static readonly Setting ModelNamespace = new("modelNamespace", "ModelNamespace", "model-namespace");

    public static readonly Setting GeneratorParameters = new("generatorParameters", "Parameters", "generator");

    public static readonly Setting TypeOverrides = new("typeOverrides", null, "type-override");
}

// Settings, each at its level, and what a run is given for them: the project's settings, the type's source, and the
// .sql file with its folder's settings file.  A level without a setting leaves its part as it was.
internal sealed class LevelSettings
{
    private const string SettingsFileName = "sqlsource.json";

    private readonly List<(SettingLevel Level, Setting Setting, string Value)> _settings = [];

    // The JSON object of the settings at the project file's level, for the harness's settings, or null when no
    // setting is at that level.
    public string? ProjectSettings => FileText(SettingLevel.ProjectFile);

    // Whether the level is a settings file, whose value is a JSON value.
    public static bool IsFile(SettingLevel level) => level is SettingLevel.ProjectFile or SettingLevel.FolderFile;

    // A setting's text as the level writes it: a JSON string at a settings file's level, and the text otherwise.
    public static string StringAt(SettingLevel level, string text) =>
        IsFile(level) ? GeneratorHarness.Json(JsonValue.Create(text)) : text;

    // Generator parameters as the level writes them: the array of their words at a settings file's level, and the
    // words otherwise.
    public static string WordsAt(SettingLevel level, string words) =>
        IsFile(level)
            ? GeneratorHarness.Json(
                new JsonArray([
                    .. words
                        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => (JsonNode?)JsonValue.Create(word)),
                ])
            )
            : words;

    // The setting at the level, with the value as that level writes it: at a settings file's level a JSON value, such
    // as "Sql", ["keep-comments"] or {"int4": "long"}, and otherwise the text of the attribute's argument or the
    // marker.
    public LevelSettings At(SettingLevel level, Setting setting, string value)
    {
        _settings.Add((level, setting, value));
        return this;
    }

    // The source with the attribute's arguments, Name = "value" or Name = Enum.Value joined by ", ", in place of
    // [SqlSourceGenerate], or the source unchanged when no setting is at the attribute's level.
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
                ?? throw new InvalidOperationException($"The attribute has no argument for {argument.Setting.Key}.")
            )
            + (
                argument.Setting.AttributeEnum is { } type
                    ? " = " + type + "." + argument.Value
                    : " = \"" + argument.Value + "\""
            )
        );
        return source.Replace(
            "[SqlSourceGenerate]",
            "[SqlSourceGenerate(" + string.Join(", ", written) + ")]",
            StringComparison.Ordinal
        );
    }

    // The file, with the file header's markers, each "-- <marker>: <value>" on its line and then a blank line, before
    // its text, and the query's markers on the line after the "-- name: <query>" line of each query named, or of every
    // query when none is named; then the settings file of its folder, when a setting is at the folder file's level.
    public SqlFile[] Files(SqlFile file, params string[] queries)
    {
        var marked = Marked(file, queries);
        if (FileText(SettingLevel.FolderFile) is not { } folderSettings)
        {
            return [marked];
        }

        var folder = file.Path[..(file.Path.LastIndexOf('/') + 1)];
        return [marked, GeneratorHarness.SettingsFile(folder + SettingsFileName, folderSettings)];
    }

    private SqlFile Marked(SqlFile file, string[] queries)
    {
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

    // The JSON object of the settings at a file's level, each key's last value, or null when none is at it.
    private string? FileText(SettingLevel level)
    {
        var settings = AtLevel(level);
        if (settings.Length == 0)
        {
            return null;
        }

        var json = new JsonObject();
        foreach (var setting in settings)
        {
            json[setting.Setting.Key] = JsonNode.Parse(setting.Value);
        }

        return GeneratorHarness.Json(json);
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
