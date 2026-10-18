namespace SqlSource.Generation.Sidecars;

/// <summary>
/// The queries of one claimed file that need an entry in its sidecar.
/// </summary>
/// <param name="NormalizedPath">The path of the <c>.sql</c> file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="Queries">
/// Each query that needs an entry, once, with the greatest output any claiming type gives it.  In file order for the
/// needs of one type, and in the ordinal order of the names once merged.  Never empty.
/// </param>
internal sealed record FileNeeds(string NormalizedPath, EquatableArray<QueryNeed> Queries);
