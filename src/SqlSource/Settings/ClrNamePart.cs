namespace SqlSource.Settings;

/// <summary>
/// One part of a <see cref="ClrTypeName" /> between periods: a namespace's name or a type's, with the type's
/// arguments.  The text does not tell a namespace from a type.
/// </summary>
/// <param name="Identifier">The part's name as written.</param>
/// <param name="Arguments">The type arguments in order, empty for a part that has none.</param>
internal sealed record ClrNamePart(string Identifier, EquatableArray<ClrTypeArgument> Arguments);
