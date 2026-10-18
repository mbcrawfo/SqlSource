namespace SqlSource.Generation.Types;

/// <summary>The C# type of a PostgreSQL type, or the reason it has none.</summary>
/// <param name="Kind">Whether the type has a C# type, and if not, why.</param>
/// <param name="Key">
/// The key that answered or failed: the type's own, or that of the element or base it was found by.
/// </param>
/// <param name="Text">
/// The C# type as a model writes it, a keyword or <c>global::</c>-qualified, or null when the type has none.
/// </param>
/// <param name="IsValueType">Whether the C# type is a value type.</param>
/// <param name="IsPublic">Whether the C# type is accessible from any assembly.</param>
/// <param name="IsObsolete">Whether the C# type is marked <c>[Obsolete]</c>.</param>
/// <param name="Detail">What a failure's message adds, or null.</param>
internal sealed record TypeMapping(
    TypeMappingKind Kind,
    string Key,
    string? Text,
    bool IsValueType,
    bool IsPublic,
    bool IsObsolete,
    string? Detail
)
{
    /// <summary>A public C# type that is not obsolete.</summary>
    public static TypeMapping Mapped(string key, string text, bool isValueType) =>
        new(TypeMappingKind.Mapped, key, text, isValueType, true, false, null);

    /// <summary>A type with no C# type, for the reason <paramref name="kind" /> gives.</summary>
    public static TypeMapping Failed(TypeMappingKind kind, string key, string? detail = null) =>
        new(kind, key, null, false, false, false, detail);
}
