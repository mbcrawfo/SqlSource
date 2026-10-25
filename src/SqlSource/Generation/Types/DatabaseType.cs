namespace SqlSource.Generation.Types;

/// <summary>
/// The C# type that the base map reads a database type as, with no level of the type override applied to it or to the
/// types it is built on: the database side of a converter.  A domain's is its base's, and an array's the array of its
/// element's.
/// </summary>
/// <param name="Text">
/// The C# type as the base map writes it, under the column of the project's driver, or null when the base map gives
/// the type none: a composite, <c>regclass</c>, a range of a subtype the map does not list, <c>hierarchyid</c>.
/// </param>
/// <param name="IsValueType">Whether that C# type is a value type.</param>
/// <param name="ElementIsValueType">
/// Whether the C# type of the element is a value type, for an array or a domain over one, its element mapped the same
/// way; false for any other type.
/// </param>
internal sealed record DatabaseType(string? Text, bool IsValueType, bool ElementIsValueType);
