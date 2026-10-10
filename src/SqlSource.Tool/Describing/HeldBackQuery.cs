namespace SqlSource.Tool.Describing;

/// <summary>
/// A query that kept its file from being written.
/// </summary>
/// <param name="Name">The query.</param>
/// <param name="Failed">Whether it failed.  False for one that is not in the run and has no current entry.</param>
internal sealed record HeldBackQuery(string Name, bool Failed);
