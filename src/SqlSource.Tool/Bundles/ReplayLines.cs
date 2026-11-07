using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SqlSource.Parsing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The log a replay's describer writes to: it passes every event on, and prints a line for each decision of the walk
/// and of the table match, and under <c>--verbose</c> for every event.
/// </summary>
/// <remarks>
/// A line is <c>sqlsource: &lt;event name&gt;: &lt;member&gt;=&lt;JSON value&gt; ...</c>, its strings JSON-quoted, and
/// is kept on one line.  An event is a decision when its name, after its engine and a dot, is <c>walk.node</c>,
/// <c>walk.column</c> or <c>table-match</c>.  Nothing is removed from it: a bundle holds no connection.
/// </remarks>
/// <param name="inner">The log of the run, which is given every event.</param>
/// <param name="output">Where the lines go: standard output.</param>
/// <param name="verbose">Whether every event is printed, and not only the decisions.</param>
internal sealed class ReplayLines(IRunLog inner, TextWriter output, bool verbose) : IRunLog
{
    private static readonly string[] Decisions = ["walk.node", "walk.column", "table-match"];

    private static readonly JsonWriterOptions ValueOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Each event is made, since one of them is printed.
    public bool Writes => true;

    public void Write(string name, params LogMember[] members)
    {
        inner.Write(name, members);
        if (!verbose && !IsDecision(name))
        {
            return;
        }

        var line = new StringBuilder(name).Append(':');
        foreach (var member in members)
        {
            _ = line.Append(' ').Append(member.Name).Append('=').Append(Json(member.Value));
        }

        output.WriteLine(Cli.LinePrefix + OneLine.Of(line.ToString()));
    }

    // The name without the engine of a describer that the tool has, and a dot.
    private static bool IsDecision(string name)
    {
        var bare = name;
        foreach (var dialect in SqlDescribableDialects.All)
        {
            var engine = SqlDialectName.Canonical(dialect) + ".";
            if (name.StartsWith(engine, StringComparison.Ordinal))
            {
                bare = name[engine.Length..];
                break;
            }
        }

        return Decisions.Contains(bare, StringComparer.Ordinal);
    }

    private static string Json(LogValue value)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, ValueOptions))
        {
            WriteValue(json, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter json, LogValue value)
    {
        switch (value)
        {
            case LogValue.Nothing:
                json.WriteNullValue();
                break;
            case LogValue.Text text:
                json.WriteStringValue(text.Value);
                break;
            case LogValue.Flag flag:
                json.WriteBooleanValue(flag.Value);
                break;
            case LogValue.Number number:
                json.WriteNumberValue(number.Value);
                break;
            case LogValue.Items items:
                json.WriteStartArray();
                foreach (var item in items.Values)
                {
                    WriteValue(json, item);
                }

                json.WriteEndArray();
                break;
            case LogValue.Members map:
                json.WriteStartObject();
                foreach (var member in map.Values)
                {
                    json.WritePropertyName(member.Name);
                    WriteValue(json, member.Value);
                }

                json.WriteEndObject();
                break;
            // The set is closed: a new kind is decided here, not let through.
            default:
                throw new InvalidOperationException($"Unknown kind of log value {value.GetType().Name}.");
        }
    }
}
