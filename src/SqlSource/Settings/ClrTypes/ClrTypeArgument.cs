namespace SqlSource.Settings.ClrTypes;

/// <summary>One type argument of a <see cref="ClrNamePart" />.</summary>
/// <param name="Type">The argument's type.</param>
/// <param name="Nullable">Whether a <c>?</c> follows it.</param>
internal sealed record ClrTypeArgument(ClrTypeName Type, bool Nullable);
