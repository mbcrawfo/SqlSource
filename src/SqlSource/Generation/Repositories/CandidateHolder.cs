using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// The field, property or primary-constructor parameter of a repository that holds the object a
/// <see cref="CandidateTier.Held" /> candidate is a member of.
/// </summary>
/// <param name="Name">The holder's name, as a value: without the <c>@</c> of an escaped keyword.</param>
/// <param name="Member">What kind of member it is: a field, a property or a parameter.</param>
/// <param name="IsStatic">Whether it is a static member.</param>
/// <param name="TypeName">
/// The holder type's simple name, such as <c>IDatabaseFactory</c>, which a message gives.
/// </param>
/// <param name="Location">Where the holder is declared.</param>
internal sealed record CandidateHolder(
    string Name,
    CandidateMember Member,
    bool IsStatic,
    string TypeName,
    LocationInfo Location
);
