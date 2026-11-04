using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SqlSource.Testing.Generator;

// The levels that give a setting of the generator, from the least specific: the project's settings file, the settings
// file of the .sql file's folder, a marker of the file header and a marker of a query.
//
// It is public, though the library's types are internal, since a public test method takes it in its rows, and C#
// refuses a public method whose parameter has a type less accessible than the method.
public enum SettingLevel
{
    ProjectFile,
    FolderFile,
    Header,
    Query,
}

// A setting by its name at each level: the key of a settings file and the marker.
internal sealed record Setting(string Key, string Marker)
{
    public static readonly Setting Output = new("output", "output");

    public static readonly Setting CollectionType = new("collectionType", "collection-type");

    public static readonly Setting InputModelSuffix = new("inputModelSuffix", "input-model-suffix");

    public static readonly Setting OutputModelSuffix = new("outputModelSuffix", "output-model-suffix");

    public static readonly Setting ModelNamespace = new("modelNamespace", "model-namespace");

    public static readonly Setting GeneratorParameters = new("generatorParameters", "generator");

    public static readonly Setting TypeOverrides = new("typeOverrides", "type-override");

    public static readonly Setting MemberLocation = new("memberLocation", "member-location");

    public static readonly Setting Visibility = new("visibility", "visibility");

    public static readonly Setting NestedClass = new("nestedClass", "nested-class");
}

// Settings, each at its level, and what a run is given for them: the project's settings, and the .sql file with its
// folder's settings file.  A level without a setting leaves its part as it was.
internal sealed class LevelSettings
{
    private const string SettingsFileName = "sqlsource.json";

    private readonly List<(SettingLevel Level, Setting Setting, string Value)> _settings = [];

    // The JSON object of the settings at the level of the project's settings file, for the harness's settings, or null
    // when no setting is at that level.
    public string? ProjectSettings => FileText(SettingLevel.ProjectFile);

    // Whether the level is a settings file, whose value is a JSON value.
    public static bool IsFile(SettingLevel level) => level is SettingLevel.ProjectFile or SettingLevel.FolderFile;

    // A setting's text as the level writes it: a JSON string at a settings file's level, and the text otherwise.
    public static string StringAt(SettingLevel level, string text) =>
        IsFile(level) ? GeneratorHarness.Json(JsonValue.Create(text)) : text;

    // Generator parameters as the level writes them: the array of their words at a settings file's level, and the
    // words otherwise.
    public static string WordsAt(SettingLevel level, string words) =>
        IsFile(level) ? GeneratorHarness.Json(GeneratorHarness.Words(words)) : words;

    // The setting at the level, with the value as that level writes it: at a settings file's level a JSON value, such
    // as "Sql", ["keep-comments"] or {"int4": "long"}, and otherwise the text of the marker.
    public LevelSettings At(SettingLevel level, Setting setting, string value)
    {
        _settings.Add((level, setting, value));
        return this;
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
