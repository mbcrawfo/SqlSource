namespace SqlSource.Generation.Types;

/// <summary>
/// A C# type that the type override names, as the compilation resolves it.  It holds no symbol, so it is equal from
/// one edit to the next until the type itself changes.
/// </summary>
/// <param name="Name">The type as <see cref="Settings.ClrTypeName.Text" /> writes it.</param>
/// <param name="Status">Whether the type is found, and if not, why.</param>
/// <param name="IsValueType">Whether the type is a value type.  False unless it is found.</param>
/// <param name="IsPublic">
/// Whether the type, its containing types and its arguments are all declared public.  False unless it is found.
/// </param>
/// <param name="IsObsolete">
/// Whether the type, a containing type or an argument is marked <c>[Obsolete]</c>.  False unless it is found.
/// </param>
internal sealed record MappedType(
    string Name,
    MappedTypeStatus Status,
    bool IsValueType,
    bool IsPublic,
    bool IsObsolete
);
