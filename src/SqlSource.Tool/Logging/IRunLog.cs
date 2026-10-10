namespace SqlSource.Tool.Logging;

/// <summary>
/// Where the events of a run go.  <see cref="RunLogFile" /> writes them to the file of <c>--log</c>, and
/// <see cref="NullRunLog" /> is what a run without one has.
/// </summary>
/// <remarks>
/// Nothing hands a log a connection's value.  A describer is given a <c>SessionRunLog</c>, which removes
/// the value all the same.
/// </remarks>
internal interface IRunLog
{
    /// <summary>Writes one event.</summary>
    /// <param name="name">The event's name: <c>run.start</c>, or <c>postgres.</c> and a describer's own.</param>
    /// <param name="members">Its members, in the order they are written.</param>
    void Write(string name, params LogMember[] members);
}
