using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The file of <c>--log</c>: JSON Lines, UTF-8 with <c>\n</c>, one object for each event, written and flushed as
/// the run goes so that a run that is stopped leaves what it did.
/// </summary>
/// <remarks>
/// <para>
/// Every object starts with <c>event</c> and <c>time</c>, the moment in UTC with milliseconds.  A string over
/// <see cref="MaxStringBytes" />, counted in UTF-8, is cut to that size between two characters, and its object
/// gains <c>"truncated": true</c>.  The writer owns these three names: a member of the event itself, not of a map
/// inside it, that is named <c>event</c>, <c>time</c> or <c>truncated</c> is written with an underscore before
/// the name, and with another for as long as a member of the event has the name that results (<c>event</c> beside
/// <c>_event</c> is written <c>__event</c>), so that no line has a key twice and no reader takes a caller's member
/// for the run's.
/// </para>
/// <para>
/// Several threads may write: an event is one write, under a lock.  A write that the system refuses does not end
/// the run: the writer keeps the <see cref="Failure" /> and writes nothing more, and <c>Cli</c> reports it when
/// the run ends.  That is a failure of the system and nothing else: an event nested deeper than the JSON writer
/// allows throws, which only a describer can cause, and which then ends as that describer's <c>SQLSRC200</c>.
/// </para>
/// </remarks>
internal sealed class RunLogFile : IRunLog, IDisposable
{
    /// <summary>The longest string an event holds, in bytes of UTF-8: 1 MiB.</summary>
    public const int MaxStringBytes = 1024 * 1024;

    // The files a run reads or writes: a log given one of these names would empty it.
    private static readonly string[] FilesOfTheRun = [".sql", ".sql.json", ".csproj", ".sln", ".slnx"];

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    // The log is for people: only what JSON must escape is escaped.
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly object _gate = new();
    private readonly MemoryStream _line = new();
    private readonly Stream _stream;
    private readonly TimeProvider _time;
    private bool _closed;

    /// <summary>Writes to a stream that is open.</summary>
    /// <param name="path">The full path of the file, for the error of a write that fails.</param>
    /// <param name="stream">Where the lines go.  The log closes it.</param>
    /// <param name="time">The clock of the run.</param>
    internal RunLogFile(string path, Stream stream, TimeProvider time)
    {
        Path = path;
        _stream = stream;
        _time = time;
    }

    public string Path { get; }

    /// <summary>Why the log stopped being written, or null while it is.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>
    /// Makes the file, or empties the one that is there.  When it cannot be, the <c>SQLSRC218</c> that says why:
    /// the system's reason, or that the name is a file of the run (a <c>.sql</c> file, a sidecar, a project or a
    /// solution, whatever the case), which is refused before anything is opened, made or emptied.  The error is
    /// at the full path, or at <paramref name="given" /> when that is no path at all.
    /// </summary>
    /// <param name="given">The path as the option or the variable gave it.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    /// <param name="createFile">Makes or empties the file at the full path and gives its stream.</param>
    /// <param name="time">The clock of the run.</param>
    public static Outcome<RunLogFile, ToolDiagnostic> Open(
        string given,
        string workingDirectory,
        Func<string, Stream> createFile,
        TimeProvider time
    )
    {
        var path = given;
        try
        {
            var full = System.IO.Path.GetFullPath(given, workingDirectory);
            path = full;
            if (FilesOfTheRun.Any(ending => full.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            {
                return NotOpened(path, "a log is not written over a .sql file, a sidecar, a project or a solution");
            }

            return new Outcome<RunLogFile, ToolDiagnostic>(new RunLogFile(path, createFile(path), time));
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // An empty path, a directory, a folder that is not there, or a file the system will not open.
            return NotOpened(path, exception.Message);
        }
    }

    private static Outcome<RunLogFile, ToolDiagnostic> NotOpened(string path, string reason) =>
        Outcome<RunLogFile, ToolDiagnostic>.Failed(
            ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, "written", reason)
        );

    public bool Writes => true;

    public void Write(string name, params LogMember[] members)
    {
        lock (_gate)
        {
            if (_closed || Failure is not null)
            {
                return;
            }

            try
            {
                _line.SetLength(0);
                using (var json = new Utf8JsonWriter(_line, Options))
                {
                    var cut = false;
                    json.WriteStartObject();
                    json.WriteString("event", Fit(name, ref cut));
                    json.WriteString(
                        "time",
                        _time.GetUtcNow().UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture)
                    );
                    foreach (var member in members)
                    {
                        json.WritePropertyName(Fit(Unreserved(member.Name, members), ref cut));
                        WriteValue(json, member.Value, ref cut);
                    }

                    if (cut)
                    {
                        json.WriteBoolean("truncated", true);
                    }

                    json.WriteEndObject();
                }

                _line.WriteByte((byte)'\n');
                _line.WriteTo(_stream);
                _stream.Flush();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Failure = exception;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            try
            {
                _stream.Dispose();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // What was buffered could not be written.
                Failure ??= exception;
            }

            _line.Dispose();
        }
    }

    // The three names the writer adds to a line are its own: a caller's member of one of them gets underscores
    // before its name, as many as it takes to reach a name that no member of the event has.
    private static string Unreserved(string name, LogMember[] members)
    {
        if (name is not ("event" or "time" or "truncated"))
        {
            return name;
        }

        // The names the event has, so that each candidate is one lookup.
        var taken = members.Select(static member => member.Name).ToHashSet(StringComparer.Ordinal);
        var underscores = 1;
        while (taken.Contains(new string('_', underscores) + name))
        {
            underscores++;
        }

        return new string('_', underscores) + name;
    }

    private static void WriteValue(Utf8JsonWriter json, LogValue value, ref bool cut)
    {
        switch (value)
        {
            case LogValue.Text text:
                json.WriteStringValue(Fit(text.Value, ref cut));
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
                    WriteValue(json, item, ref cut);
                }

                json.WriteEndArray();
                break;
            case LogValue.Members members:
                json.WriteStartObject();
                foreach (var member in members.Values)
                {
                    json.WritePropertyName(Fit(member.Name, ref cut));
                    WriteValue(json, member.Value, ref cut);
                }

                json.WriteEndObject();
                break;
            default:
                json.WriteNullValue();
                break;
        }
    }

    // A string as the file holds it: at most MaxStringBytes of UTF-8, cut between two characters, and with half a
    // character, which JSON cannot hold, written as the replacement character.
    private static string Fit(string text, ref bool cut)
    {
        // A char is at most three bytes of UTF-8, so a short text with no half characters is written as it is.
        if (text.Length <= MaxStringBytes / 3 && text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') < 0)
        {
            return text;
        }

        var kept = new StringBuilder(Math.Min(text.Length, MaxStringBytes));
        var bytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxStringBytes)
            {
                cut = true;
                break;
            }

            bytes += rune.Utf8SequenceLength;
            _ = kept.Append(rune.ToString());
        }

        return kept.ToString();
    }
}
