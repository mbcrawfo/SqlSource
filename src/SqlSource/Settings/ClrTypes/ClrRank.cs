namespace SqlSource.Settings.ClrTypes;

/// <summary>
/// One array rank of a <see cref="ClrTypeName" />: <c>[]</c>, <c>[,]</c>, or either after a <c>?</c>.
/// </summary>
/// <param name="NullableElement">
/// Whether a <c>?</c> comes before it, making the element nullable: <c>int?[]</c>.
/// </param>
/// <param name="Dimensions">The number of dimensions, one more than the commas inside the brackets.</param>
internal sealed record ClrRank(bool NullableElement, int Dimensions);
