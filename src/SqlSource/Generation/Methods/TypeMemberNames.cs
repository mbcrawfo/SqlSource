namespace SqlSource.Generation.Methods;

/// <summary>
/// The names that a type whose methods go on itself already has, which no method of its own may take.
/// </summary>
/// <param name="TypeKey">The type, as <c>HintName.NameOf</c> gives it.</param>
/// <param name="Names">
/// The names of the type, its members, its nested types and its type parameters, sorted ordinally, as
/// <see cref="DeclaredType.TakenNames" /> holds them.
/// </param>
internal sealed record TypeMemberNames(string TypeKey, EquatableArray<string> Names);
