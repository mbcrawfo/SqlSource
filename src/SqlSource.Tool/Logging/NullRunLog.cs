namespace SqlSource.Tool.Logging;

/// <summary>The log of a run that has none.</summary>
internal sealed class NullRunLog : IRunLog
{
    private NullRunLog() { }

    public static NullRunLog Instance { get; } = new();

    public void Write(string name, params LogMember[] members)
    {
        // Nothing is kept: the run has no --log.
    }
}
