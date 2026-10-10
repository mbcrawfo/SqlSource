namespace SqlSource.Tool.Logging;

/// <summary>
/// What a run says of itself beside its errors and its summary.  <c>Reporter</c> carries it, so every step that
/// can report can say what it did.
/// </summary>
/// <param name="log">The log of the run.</param>
internal sealed class RunTrace(IRunLog log)
{
    public IRunLog Log => log;

    /// <summary>
    /// The counts of each database, as <c>run.end</c> holds them.  <c>describe</c> sets it when it has them, and
    /// <c>Cli</c>, which knows the exit code, writes the event.
    /// </summary>
    public LogValue Databases { get; set; } = LogValue.ListOf([]);

    /// <summary>
    /// The same trace with one connection's value removed from everything said through it.  What the run says of
    /// a database once it has the connection goes through one: a server's version, a driver's name and a setting
    /// are a describer's text, as a failure's lines are.
    /// </summary>
    public RunTrace Without(string secret) => new(new SessionRunLog(log, secret));
}
