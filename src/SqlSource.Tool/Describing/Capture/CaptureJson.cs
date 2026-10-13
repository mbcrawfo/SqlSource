using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// A capture as text: the file of a fixture and of a bug bundle, and the one way a request and an answer are
/// written.
/// </summary>
/// <remarks>
/// The file is indented and its line ends are <c>\n</c> on every platform, so that a capture under version control
/// differs line by line.  The encoder is the relaxed one, as the log's is: a capture holds SQL, and a reviewer reads
/// its quotes and angle brackets as they are.
/// </remarks>
internal static class CaptureJson
{
    private const string VersionKey = "captureVersion";
    private const string EngineKey = "engine";
    private const string CallsKey = "calls";
    private const string MethodKey = "method";
    private const string RequestKey = "request";
    private const string AnswerKey = "answer";

    private static readonly JsonWriterOptions FileOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // A key is escaped as a request is written, so that a request with no null member has its own text for a key.
    private static readonly JsonWriterOptions KeyOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// How every request and every answer is written: camel-case names, every member, a null one included, and
    /// no indentation.
    /// </summary>
    public static JsonSerializerOptions Options { get; } =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    /// <summary>A request or an answer as a capture keeps it.</summary>
    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    /// <summary>
    /// The text that stands for a request when a recorded answer is looked up: two requests with one key are one
    /// question.  A request that was read from a file and the same request asked anew have one key, whatever the
    /// file's indentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A key is the request without its null members, at every depth, so that a request's record can gain a
    /// nullable member: a call that was recorded before the member was there answers the request that has it as
    /// null.  A null in a list is a value of the list and stays, and so does every value that is not null, a zero
    /// and an empty list among them.
    /// </para>
    /// <para>
    /// A key is made anew at each replay, of the recorded request and of the asked one alike, and is in no file:
    /// the file holds the request whole, and that is the contract.
    /// </para>
    /// </remarks>
    public static string KeyOf(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, KeyOptions))
        {
            WriteKey(json, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// A request or an answer of a capture on one line, with every member: what a message shows of it.
    /// </summary>
    public static string TextOf(JsonElement element) => JsonSerializer.Serialize(element, Options);

    /// <summary>The file of a capture.</summary>
    public static string Write(DescribeCapture capture)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, FileOptions))
        {
            json.WriteStartObject();
            json.WriteNumber(VersionKey, DescribeCapture.Version);
            json.WriteString(EngineKey, capture.Engine);
            json.WriteStartArray(CallsKey);
            foreach (var call in capture.Calls)
            {
                json.WriteStartObject();
                json.WriteString(MethodKey, call.Method);
                json.WritePropertyName(RequestKey);
                call.Request.WriteTo(json);
                json.WritePropertyName(AnswerKey);
                call.Answer.WriteTo(json);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        // The writer indents with the platform's line end.  A line break inside a value is written as an escape,
        // so only the writer's own are replaced.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>
    /// Reads the file of a capture, or says in a sentence why the text is none.  A <c>captureVersion</c> that
    /// this version does not read is named.
    /// </summary>
    public static Outcome<DescribeCapture, string> Read(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Failed($"The capture is not JSON: {exception.Message}");
        }

        using (document)
        {
            return ReadRoot(document.RootElement);
        }
    }

    // Options that leave a null out do nothing for an element, so the members are written one by one.
    private static void WriteKey(Utf8JsonWriter json, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            json.WriteStartObject();
            foreach (var member in element.EnumerateObject())
            {
                if (member.Value.ValueKind != JsonValueKind.Null)
                {
                    json.WritePropertyName(member.Name);
                    WriteKey(json, member.Value);
                }
            }

            json.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            json.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                WriteKey(json, item);
            }

            json.WriteEndArray();
        }
        else
        {
            element.WriteTo(json);
        }
    }

    private static Outcome<DescribeCapture, string> ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failed("The capture is not a JSON object.");
        }

        if (
            !root.TryGetProperty(VersionKey, out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var number)
        )
        {
            return Failed("The capture has no captureVersion.");
        }

        if (number != DescribeCapture.Version)
        {
            return Failed(
                $"The capture's captureVersion is {number}, and this version of sqlsource reads "
                    + $"{DescribeCapture.Version}."
            );
        }

        if (StringOf(root, EngineKey) is not { } engine)
        {
            return Failed("The capture has no engine.");
        }

        return root.TryGetProperty(CallsKey, out var calls) && calls.ValueKind == JsonValueKind.Array
            ? ReadCalls(new DescribeCapture(engine), calls)
            : Failed("The capture has no calls.");
    }

    private static Outcome<DescribeCapture, string> ReadCalls(DescribeCapture capture, JsonElement calls)
    {
        var position = 0;
        foreach (var call in calls.EnumerateArray())
        {
            position++;
            if (ReadCall(call) is not { } read)
            {
                return Failed($"The call {position} of the capture has no method, request or answer.");
            }

            capture.Add(read);
        }

        return Outcome<DescribeCapture, string>.Of(capture);
    }

    // An element is cloned: the document it was read from is disposed when the capture is returned.
    private static CaptureCall? ReadCall(JsonElement call) =>
        call.ValueKind == JsonValueKind.Object
        && StringOf(call, MethodKey) is { } method
        && call.TryGetProperty(RequestKey, out var request)
        && call.TryGetProperty(AnswerKey, out var answer)
            ? new CaptureCall(method, request.Clone(), answer.Clone())
            : null;

    private static string? StringOf(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Outcome<DescribeCapture, string> Failed(string reason) =>
        Outcome<DescribeCapture, string>.Failed(reason);
}
