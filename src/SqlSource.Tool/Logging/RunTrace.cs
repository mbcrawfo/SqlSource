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
/// <param name="secret">A connection's secret that is removed from every line, or null.</param>
internal sealed class RunTrace(IRunLog log, TextWriter? verbose = null, ConnectionSecret? secret = null)
{
    private const string Prefix = "sqlsource: ";

    public IRunLog Log => log;

    /// <summary>
    /// Says one line under <c>--verbose</c>, and nothing without it.  The text is kept on one line: it holds
    /// paths and names that the tool did not write.  A secret is taken out before and after, since a line break
    /// written as a space can make a part that holds a space.
    /// </summary>
    /// <param name="text">The line, without its prefix.</param>
    public void Say(string text) =>
        verbose?.WriteLine(
            Prefix + (secret is null ? OneLine.Of(text) : Redaction.Of(OneLine.Of(Redaction.Of(text, secret)), secret))
        );

    /// <summary>
    /// The same trace with one connection's secret removed from everything said through it.  What the run says of
    /// a database once it has the connection goes through one: a server's version, a driver's name and a setting
    /// are a describer's text, as a failure's lines are.
    /// </summary>
    /// <param name="connection">The connection's value and its secret parts.</param>
    public RunTrace Without(ConnectionSecret connection) =>
        new(new SessionRunLog(log, connection), verbose, connection);
}
