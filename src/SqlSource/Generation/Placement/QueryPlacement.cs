using SqlSource.Settings;

namespace SqlSource.Generation.Placement;

/// <summary>
/// Where one query's members go and how visible they are, as <see cref="TypePlacement.PlacementOf" /> resolves it for
/// its type.
/// </summary>
/// <param name="Location">Where the members go.</param>
/// <param name="Visibility">
/// The members' accessibility on the type or in the extension class.  Under <see cref="MemberLocation.NestedClass" />
/// it is the nested class's own, which every query of the class shares, and the members in it are public.
/// </param>
/// <param name="NestedClass">
/// The name of the nested class under <see cref="MemberLocation.NestedClass" />, and null under any other location.
/// </param>
internal readonly record struct QueryPlacement(MemberLocation Location, Visibility Visibility, string? NestedClass);
