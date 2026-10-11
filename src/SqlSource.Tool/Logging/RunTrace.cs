using System.IO;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Logging;

/// <summary>
/// What a run says of itself beside its errors and its summary: the events of its log, and under <c>--verbose</c>
/// a line of prose for the person watching.  <c>Reporter</c> carries it, so every step that can report can say
/// what it did.
/// </summary>
/// <param name="log">The log of the run.</param>
/// <param name="verbose">Standard output, when the run is verbose.</param>
/// <param name="secret">A connection's value that is removed from every line, or null.</param>
internal sealed class RunTrace(IRunLog log, TextWriter? verbose = null, string? secret = null)
{
    private const string Prefix = "sqlsource: ";

    public IRunLog Log => log;

    /// <summary>
    /// Says one line under <c>--verbose</c>, and nothing without it.  The text is kept on one line: it holds
    /// paths and names that the tool did not write.
    /// </summary>
    /// <param name="text">The line, without its prefix.</param>
    public void Say(string text) =>
        verbose?.WriteLine(Prefix + OneLine.Of(secret is null ? text : Redaction.Of(text, secret)));

    /// <summary>
    /// The same trace with one connection's value removed from everything said through it.  What the run says of
    /// a database once it has the connection goes through one: a server's version, a driver's name and a setting
    /// are a describer's text, as a failure's lines are.
    /// </summary>
    /// <param name="value">The connection's value.</param>
    public RunTrace Without(string value) => new(new SessionRunLog(log, value), verbose, value);
}
