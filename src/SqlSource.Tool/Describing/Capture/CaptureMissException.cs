using System;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// A describer under replay asked a question that its capture does not answer: one the capture does not hold, one
/// whose answers were all given, or one whose answer is not of the shape the describer reads.
/// </summary>
/// <remarks>
/// <para>
/// It is a bug or a stale capture, never something a server or a user can cause.  Under a run of the tool it ends
/// the run with <c>SQLSRC200</c>, as any exception of a describer does; under a test it fails the test with the
/// question in its message, so that a change to a describer's calls is a visible update of its fixtures.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException" /> and has the standard constructors for the reasons
/// <c>DescriberFaultException</c> gives: S3871, CA1515 and CA1032.
/// </para>
/// </remarks>
internal sealed class CaptureMissException : InvalidOperationException
{
    public CaptureMissException()
        : this("The capture does not answer a question that was asked.") { }

    public CaptureMissException(string message)
        : base(message) { }

    public CaptureMissException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The capture holds no call of the method with the request, or every one was given out.</summary>
    /// <param name="method">The name of the question.</param>
    /// <param name="request">
    /// The request's JSON with every member, as <see cref="CaptureJson.TextOf" /> gives it.
    /// </param>
    public static CaptureMissException NoAnswer(string method, string request) =>
        new($"The capture has no further answer to '{method}' for the request {request}.");

    /// <summary>The capture holds an answer that cannot be read as the type the describer asked for.</summary>
    public static CaptureMissException Unreadable(string method, Type answer, string reason) =>
        new($"The capture's answer to '{method}' cannot be read as {answer.Name}: {reason}");
}
