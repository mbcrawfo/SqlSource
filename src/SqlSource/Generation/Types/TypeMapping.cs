namespace SqlSource.Generation.Types;

/// <summary>The C# type of a PostgreSQL type, or the reason it has none.</summary>
/// <param name="Kind">Whether the type has a C# type, and if not, why.</param>
/// <param name="Key">
/// The key that answered or failed: the type's own, or that of the element or base it was found by.
/// </param>
/// <param name="Text">
/// The C# type as a model writes it, a keyword or <c>global::</c>-qualified, or null when the type has none.  A
/// failure of a type the override names keeps that type here, for its message.
/// </param>
/// <param name="IsValueType">Whether the C# type is a value type.</param>
/// <param name="IsPublic">Whether the C# type is accessible from any assembly.</param>
/// <param name="WarningIds">The ids of the compiler's warnings that a use of the C# type gives.</param>
/// <param name="Detail">
/// What a failure's message adds, or null: for <see cref="TypeMappingKind.CastToOid" />,
/// <see cref="TypeMappingKind.CastToText" /> and <see cref="TypeMappingKind.NeedsNpgsql10" />, the type to cast to,
/// an array of it for an array; for <see cref="TypeMappingKind.DiffersByDriver" />, the entry of the type
/// override for each driver whose C# type is not of <c>NpgsqlTypes</c>, or null when neither is; and for a type the
/// override names, found or not, the place that wrote the entry.
/// </param>
internal sealed record TypeMapping(
    TypeMappingKind Kind,
    string Key,
    string? Text,
    bool IsValueType,
    bool IsPublic,
    EquatableArray<string> WarningIds,
    string? Detail
)
{
    /// <summary>A public C# type whose use gives no warning.</summary>
    public static TypeMapping Mapped(string key, string text, bool isValueType) =>
        new(TypeMappingKind.Mapped, key, text, isValueType, true, EquatableArray<string>.Empty, null);

    /// <summary>A type with no C# type, for the reason <paramref name="kind" /> gives.</summary>
    public static TypeMapping Failed(TypeMappingKind kind, string key, string? detail = null) =>
        new(kind, key, null, false, false, EquatableArray<string>.Empty, detail);
}
