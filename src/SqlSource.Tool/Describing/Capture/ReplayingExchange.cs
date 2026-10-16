using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// The exchange that replays: it answers each question from a capture and never asks the engine, so a describer
/// gives the description it gave when the capture was recorded, with no database.
/// </summary>
/// <remarks>
/// <para>
/// A call is found by its method and by its request as <see cref="CaptureJson.KeyOf" /> writes it, which leaves out
/// its null members so that a request's record can gain a nullable one.  A question the
/// capture holds more than once gets its answers in the capture's order, one each time it is asked.  A question
/// with no answer left is a <see cref="CaptureMissException" />.
/// </para>
/// <para>
/// A capture that holds more than was asked is no error: <see cref="Unused" /> says what was left.  A capture may
/// be older than a record: an answer that lacks a nullable member is read with null there, and one that lacks a
/// member of a reference type that is not nullable is a <see cref="CaptureMissException" /> that names the member
/// (<see cref="AnswerNulls" />).  A missing member of a value type is read as its default and is not found.
/// </para>
/// </remarks>
/// <param name="capture">The calls to answer from.  It is read once, when the exchange is made.</param>
internal sealed class ReplayingExchange(DescribeCapture capture) : IDescribeExchange
{
    private readonly List<Recorded> _calls =
    [
        .. capture.Calls.Select(static call => new Recorded(call, CaptureJson.KeyOf(call.Request))),
    ];

    /// <summary>The calls of the capture that were not asked, in the capture's order.</summary>
    public IReadOnlyList<CaptureCall> Unused =>
        [.. _calls.Where(static recorded => !recorded.Given).Select(static recorded => recorded.Call)];

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        var asked = CaptureJson.ToElement(request);
        var key = CaptureJson.KeyOf(asked);
        var recorded =
            _calls.Find(candidate =>
                !candidate.Given
                && string.Equals(candidate.Call.Method, method, StringComparison.Ordinal)
                && string.Equals(candidate.Key, key, StringComparison.Ordinal)
            ) ?? throw CaptureMissException.NoAnswer(method, CaptureJson.TextOf(asked));
        recorded.Given = true;
        return Task.FromResult(Read<TAnswer>(method, recorded.Call.Answer));
    }

    private static TAnswer Read<TAnswer>(string method, JsonElement answer)
    {
        TAnswer? read;
        try
        {
            read = answer.Deserialize<TAnswer>(CaptureJson.Options);
        }
        catch (JsonException exception)
        {
            throw CaptureMissException.Unreadable(method, typeof(TAnswer), exception.Message);
        }

        if (read is null)
        {
            throw CaptureMissException.Unreadable(method, typeof(TAnswer), "it is null");
        }

        return AnswerNulls.Find(read) is { } member
            ? throw CaptureMissException.Unreadable(
                method,
                typeof(TAnswer),
                $"its member '{member}' is null or missing"
            )
            : read;
    }

    // A call of the capture with its key, and whether its answer was given out.
    private sealed class Recorded(CaptureCall call, string key)
    {
        public CaptureCall Call => call;

        public string Key => key;

        public bool Given { get; set; }
    }
}
