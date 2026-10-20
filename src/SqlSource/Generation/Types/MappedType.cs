namespace SqlSource.Generation.Types;

/// <summary>
/// A C# type that the type override names, as the compilation resolves it.  It holds no symbol, so it is equal from
/// one edit to the next until the type itself changes.
/// </summary>
/// <param name="Name">The type as <see cref="Settings.ClrTypes.ClrTypeName.Text" /> writes it.</param>
/// <param name="Status">Whether the type is found, and if not, why.</param>
/// <param name="IsValueType">Whether the type is a value type.  False unless it is found.</param>
/// <param name="IsPublic">
/// Whether the type, its containing types and its arguments are all declared public.  False unless it is found.
/// </param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a use of the type gives, for the type, a containing type or an argument
/// marked <c>[Obsolete]</c> or <c>[Experimental]</c>, in ordinal order, each once.  Empty unless it is found.
/// </param>
internal sealed record MappedType(
    string Name,
    MappedTypeStatus Status,
    bool IsValueType,
    bool IsPublic,
    EquatableArray<string> WarningIds
);
