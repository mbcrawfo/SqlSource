using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// What a run that records keeps of its one session, for <c>diagnose</c>: every call the describer made, the
/// session's server, and the capture as it is written once the session has closed.
/// </summary>
/// <remarks>
/// <see cref="DatabaseRuns" /> fills it and writes it, so that the connection's secret, which the writer needs to
/// refuse a capture that holds it (<see cref="CaptureJson.Write(DescribeCapture, ConnectionSecret)" />), never leaves
/// the run.  A capture is of one session, so a run given one has a single database to describe.
/// </remarks>
/// <param name="engine">The canonical name of the dialect of the database the run describes.</param>
internal sealed class RunCapture(string engine)
{
    /// <summary>Every call of the session, in the order the describer made them.</summary>
    public DescribeCapture Capture { get; } = new(engine);

    /// <summary>
    /// The capture as <see cref="CaptureJson.Write(DescribeCapture, ConnectionSecret)" /> wrote it, once the session
    /// closed; null when no session was opened, or the run ended before it closed.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>The server of the session, once it was opened.</summary>
    public ServerInfo? Server { get; set; }
}
