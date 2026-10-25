namespace SqlSource.Generation;

/// <summary>
/// A type as <see cref="TargetTypeReader" /> reads it, before the project's properties join.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="TakenNames">
/// The names that the type's nested class, and its methods when they go on the type, cannot have, sorted ordinally:
/// those of the type, its members and its type parameters.  Empty in <see cref="Settings.MemberPlacement.Direct" />
/// placement under <see cref="Settings.MethodPlacement.ExtensionClass" />, which has neither.
/// </param>
internal sealed record DeclaredType(TargetType Type, EquatableArray<string> TakenNames);
