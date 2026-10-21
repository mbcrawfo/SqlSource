namespace SqlSource.Generation;

/// <summary>
/// A type as <see cref="TargetTypeReader" /> reads it, before the project's properties join.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="TakenNames">
/// The names that the type's nested class cannot have, sorted ordinally: those of the type, its members and its type
/// parameters.  Empty in <see cref="Settings.MemberPlacement.Direct" /> placement, which has no nested class.
/// </param>
internal sealed record DeclaredType(TargetType Type, EquatableArray<string> TakenNames);
