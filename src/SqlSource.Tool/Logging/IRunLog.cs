namespace SqlSource.Tool.Logging;

/// <summary>
/// Where the events of a run go.  <see cref="RunLogFile" /> writes them to the file of <c>--log</c>, and
/// <see cref="NullRunLog" /> is what a run without one has.
/// </summary>
/// <remarks>
/// Nothing hands a log a connection's value.  A describer is given a <see cref="SessionRunLog" />, which removes
/// the value all the same.
/// </remarks>
internal interface IRunLog
{
    /// <summary>
    /// Whether the log keeps what it is given, so that a step whose events cost something to make can skip them.
    /// </summary>
    bool Writes { get; }

    /// <summary>Writes one event.</summary>
    /// <param name="name">
    /// The event's name: one of the run's, as <c>run.start</c>, or a describer's own after its engine and a dot,
    /// <c>postgres.</c> or <c>mssql.</c>.
    /// </param>
    /// <param name="members">Its members, in the order they are written.</param>
    void Write(string name, params LogMember[] members);
}
