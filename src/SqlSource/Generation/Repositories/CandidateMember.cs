namespace SqlSource.Generation.Repositories;

/// <summary>
/// What kind of member of a repository a <see cref="ConnectionCandidate" /> is.
/// </summary>
internal enum CandidateMember
{
    Field,
    Property,
    Method,

    /// <summary>A parameter of the type's primary constructor.</summary>
    Parameter,
}
