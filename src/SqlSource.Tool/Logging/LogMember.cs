namespace SqlSource.Tool.Logging;

/// <summary>
/// One member of an event or of a map.  People and <c>replay</c>'s lines read a member by its name: one is added, and
/// never renamed.
/// </summary>
internal readonly record struct LogMember(string Name, LogValue Value);
