namespace SqlSource.Snapshot;

/// <summary>
/// A type under an engine this version does not know.  The format asks every engine for a name, and nothing else of
/// the type is read.
/// </summary>
internal sealed record OtherEngineType(string Name) : SidecarType(Name);
