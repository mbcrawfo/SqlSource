using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SqlSource.Snapshot;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The bundle's <c>bundle.json</c> as text.
/// </summary>
/// <remarks>
/// The file is written as a capture is: indented, with <c>\n</c> line ends on every platform and a final line break,
/// and with the relaxed encoder, so a reader sees a version's quotes and angle brackets as they are.  Its keys are
/// the members of <see cref="BundleInfo" /> in camel case, and its <c>outcome</c> an object with a <c>kind</c>, and an
/// <c>id</c> and a <c>step</c> for a failure.  A <c>bundleVersion</c> newer than <see cref="Version" /> is named
/// before any other member is read, since a newer form may have changed them.  A <c>formatVersion</c> newer than the
/// sidecar format this tool writes is named once the file reads, since the bundle's expected answer is of that format.
/// </remarks>
internal static class BundleJson
{
    /// <summary>The version of the bundle's form that this tool writes and reads.</summary>
    public const int Version = 1;

    private const string VersionKey = "bundleVersion";
    private const string RenamedKey = "renamed";
    private const string ToolVersionKey = "toolVersion";
    private const string FormatVersionKey = "formatVersion";
    private const string EngineKey = "engine";
    private const string ServerVersionKey = "serverVersion";
    private const string DriverVersionKey = "driverVersion";
    private const string RuntimeKey = "runtime";
    private const string OperatingSystemKey = "operatingSystem";
    private const string OutcomeKey = "outcome";
    private const string KindKey = "kind";
    private const string IdKey = "id";
    private const string StepKey = "step";
    private const string EntryKind = "entry";
    private const string FailureKind = "failure";

    private static readonly JsonWriterOptions FileOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The file of the bundle's info.</summary>
    public static string Write(BundleInfo info)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, FileOptions))
        {
            json.WriteStartObject();
            json.WriteNumber(VersionKey, info.BundleVersion);
            json.WriteBoolean(RenamedKey, info.Renamed);
            json.WriteString(ToolVersionKey, info.ToolVersion);
            json.WriteNumber(FormatVersionKey, info.FormatVersion);
            json.WriteString(EngineKey, info.Engine);
            json.WriteString(ServerVersionKey, info.ServerVersion);
            json.WriteString(DriverVersionKey, info.DriverVersion);
            json.WriteString(RuntimeKey, info.Runtime);
            json.WriteString(OperatingSystemKey, info.OperatingSystem);
            json.WriteStartObject(OutcomeKey);
            switch (info.Outcome.Kind)
            {
                case BundleOutcomeKind.Entry:
                    json.WriteString(KindKey, EntryKind);
                    break;
                case BundleOutcomeKind.Failure:
                    json.WriteString(KindKey, FailureKind);
                    json.WriteString(IdKey, info.Outcome.Id);
                    json.WriteString(StepKey, info.Outcome.Step);
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(info.Outcome.Kind);
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        // The writer indents with the platform's line end.  A line break inside a value is written as an escape,
        // so only the writer's own are replaced.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>
    /// Reads the file of a bundle's info, or gives the error at the bundle: <c>SQLSRC289</c> for a
    /// <c>bundleVersion</c> or a <c>formatVersion</c> newer than this tool reads, and <c>SQLSRC288</c> naming what
    /// else is wrong.
    /// </summary>
    /// <param name="text">The text of <c>bundle.json</c>.</param>
    /// <param name="path">The full path of the bundle, where the error is.</param>
    public static Outcome<BundleInfo, ToolDiagnostic> Read(string text, string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Failed(path, $"{BundleFiles.Info} is not JSON: {exception.Message}");
        }

        using (document)
        {
            return ReadRoot(document.RootElement, path);
        }
    }

    private static Outcome<BundleInfo, ToolDiagnostic> ReadRoot(JsonElement root, string path)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failed(path, $"{BundleFiles.Info} is not a JSON object");
        }

        var members = new Members(root, BundleFiles.Info);
        var version = members.Int(VersionKey);
        if (version > Version)
        {
            return Outcome<BundleInfo, ToolDiagnostic>.Failed(
                BundleErrors.OfNewerTool(path, VersionKey, version.Value, Version)
            );
        }

        if (version < Version)
        {
            return Failed(path, $"the {VersionKey} of {BundleFiles.Info} is {version}, which no sqlsource writes");
        }

        var renamed = members.Bool(RenamedKey);
        var toolVersion = members.String(ToolVersionKey);
        var formatVersion = members.Int(FormatVersionKey);
        var engine = members.String(EngineKey);
        var serverVersion = members.String(ServerVersionKey);
        var driverVersion = members.NullableString(DriverVersionKey);
        var runtime = members.String(RuntimeKey);
        var operatingSystem = members.String(OperatingSystemKey);
        var outcome = members.Object(OutcomeKey) is { } element ? ReadOutcome(element, members) : null;

        if (members.Wrong is { } wrong)
        {
            return Failed(path, wrong);
        }

        // The sidecar format is versioned apart from the bundle's form, so a newer one is named once the file reads.
        return formatVersion > SidecarFormat.Version
            ? Outcome<BundleInfo, ToolDiagnostic>.Failed(
                BundleErrors.OfNewerTool(path, FormatVersionKey, formatVersion.Value, SidecarFormat.Version)
            )
            : Outcome<BundleInfo, ToolDiagnostic>.Of(
                new BundleInfo(
                    version!.Value,
                    renamed!.Value,
                    toolVersion!,
                    formatVersion!.Value,
                    engine!,
                    serverVersion!,
                    driverVersion,
                    runtime!,
                    operatingSystem!,
                    outcome!
                )
            );
    }

    // The outcome, or null with what is wrong with it kept by the members of the file.
    private static BundleOutcome? ReadOutcome(JsonElement element, Members file)
    {
        var members = new Members(element, $"the {OutcomeKey} of {BundleFiles.Info}");
        var kind = members.String(KindKey);
        var outcome = kind switch
        {
            EntryKind => BundleOutcome.Entry,
            FailureKind when members.String(IdKey) is { } id => BundleOutcome.Failure(
                id,
                members.NullableString(StepKey)
            ),
            _ => null,
        };

        if (kind is not (null or EntryKind or FailureKind))
        {
            members.Keep(
                $"the {KindKey} of the {OutcomeKey} of {BundleFiles.Info} is '{kind}', which is neither "
                    + $"'{EntryKind}' nor '{FailureKind}'"
            );
        }

        if (members.Wrong is { } wrong)
        {
            file.Keep(wrong);
            return null;
        }

        return outcome;
    }

    private static Outcome<BundleInfo, ToolDiagnostic> Failed(string path, string reason) =>
        Outcome<BundleInfo, ToolDiagnostic>.Failed(BundleErrors.CannotBeRead(path, reason));

    // Reads the members of one object, and keeps the first that is missing or of another type.
    private sealed class Members(JsonElement element, string owner)
    {
        public string? Wrong { get; private set; }

        // Keeps the reason unless one was kept before.
        public void Keep(string reason) => Wrong ??= reason;

        public int? Int(string key) =>
            Member(
                key,
                "a whole number",
                static value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _)
            )
                ?.GetInt32();

        public bool? Bool(string key) =>
            Member(key, "true or false", static value => value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                ?.GetBoolean();

        public string? String(string key) =>
            Member(key, "a string", static value => value.ValueKind == JsonValueKind.String)?.GetString();

        // A member that must be there, as a string or as null.
        public string? NullableString(string key) =>
            element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Null ? null : String(key);

        public JsonElement? Object(string key) =>
            Member(key, "an object", static value => value.ValueKind == JsonValueKind.Object);

        private JsonElement? Member(string key, string what, Func<JsonElement, bool> fits)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                Keep($"{owner} has no {key}");
                return null;
            }

            if (!fits(value))
            {
                Keep($"the {key} of {owner} is not {what}");
                return null;
            }

            return value;
        }
    }
}
