namespace SqlSource.Generation.Types;

/// <summary>The C# type of one parameter or column of a query, which its model's property has.</summary>
/// <param name="Text">The C# type as a model writes it, a keyword or <c>global::</c>-qualified.</param>
/// <param name="IsValueType">Whether the C# type is a value type.</param>
/// <param name="IsPublic">Whether the C# type is accessible from any assembly.</param>
/// <param name="IsObsolete">Whether the C# type is marked <c>[Obsolete]</c>.</param>
/// <param name="Key">
/// The key that answered: of the type override's entry, or of the row of the base map, which may be the key of an
/// array's element or of a domain's base.
/// </param>
internal sealed record PropertyType(string Text, bool IsValueType, bool IsPublic, bool IsObsolete, string Key);
