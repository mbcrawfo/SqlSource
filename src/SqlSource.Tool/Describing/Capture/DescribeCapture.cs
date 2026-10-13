using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// Every call a describer made while it described, in the order it made them.  <c>CapturingExchange</c> fills one,
/// <see cref="CaptureJson" /> writes and reads it, and <c>ReplayingExchange</c> answers from it.
/// </summary>
/// <remarks>
/// It is for one thread, as a session is: the run describes one query at a time.
/// </remarks>
/// <param name="engine">The canonical name of the dialect whose describer made the calls.</param>
internal sealed class DescribeCapture(string engine)
{
    /// <summary>
    /// The version of the file.  It rises when a capture of the version before can no longer be read.
    /// </summary>
    public const int Version = 1;

    private readonly List<CaptureCall> _calls = [];

    public string Engine => engine;

    public IReadOnlyList<CaptureCall> Calls => _calls;

    public void Add(CaptureCall call) => _calls.Add(call);
}
