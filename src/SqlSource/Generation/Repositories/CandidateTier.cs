namespace SqlSource.Generation.Repositories;

/// <summary>
/// Where a <see cref="ConnectionCandidate" /> is found, which is also how far from the repository it is looked for.
/// </summary>
internal enum CandidateTier
{
    /// <summary>A member of the repository, or a parameter of its primary constructor.</summary>
    Own,

    /// <summary>A marked member of the type of a field, property or parameter that the repository holds.</summary>
    Held,

    /// <summary>A marked static member of a type of the project.</summary>
    Global,
}
