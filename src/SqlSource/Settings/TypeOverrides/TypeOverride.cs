using SqlSource.Settings.ClrTypes;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// One entry of the type override: the C# type a database type maps to.
/// </summary>
/// <param name="Key">
/// The type's key, as one engine reads it: <see cref="TypeKeys.TryNormalizePostgres" /> or
/// <see cref="TypeKeys.TryNormalizeSqlServer" /> gives it.
/// </param>
/// <param name="Type">
/// The C# type as <see cref="ClrTypeName.Text" /> writes it, or null for <c>default</c>, which takes the key out of
/// the map.
/// </param>
/// <param name="Place">Where the entry was written.</param>
internal sealed record TypeOverride(string Key, string? Type, SettingPlace Place);
